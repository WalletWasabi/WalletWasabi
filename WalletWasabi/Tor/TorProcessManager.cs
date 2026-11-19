using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using WalletWasabi.BundledApps;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Services;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events.StatusEvents;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor;

public class TorProcessManager(EventBus eventBus)
{
	/// <summary>Timeout for Tor bootstrap process to finish in milliseconds.</summary>
	private const int BootstrapTimeoutMs = 30_000;

	private static readonly byte[] NoAuthHandshakeMsg = [
		0x05, // Version
		0x01, // One method
		0x00, // No authentication
	];

	private readonly EventBus _eventBus = eventBus;

	/// <param name="arguments">Command line arguments to start Tor OS process with.</param>
	public virtual Process StartProcess(TorSettings settings, string arguments)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = settings.TorBinaryFilePath,
			Arguments = arguments,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			WorkingDirectory = settings.TorBinaryDir
		};

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var env = startInfo.EnvironmentVariables;

			env["LD_LIBRARY_PATH"] = !env.ContainsKey("LD_LIBRARY_PATH") || string.IsNullOrEmpty(env["LD_LIBRARY_PATH"])
				? settings.TorBinaryDir
				: settings.TorBinaryDir + Path.PathSeparator + env["LD_LIBRARY_PATH"];

			Logger.LogDebug($"Environment variable 'LD_LIBRARY_PATH' set to: '{env["LD_LIBRARY_PATH"]}'.");
		}

		Logger.LogInfo($"Starting Tor from folder '{settings.TorBinaryDir}' with arguments '{arguments}'…");

		var process = new Process()
		{
			StartInfo = startInfo,
		};

		process.StartWithExceptionLogging();

		return process;
	}

	public virtual async Task WaitForProcessExitAsync(Process process, CancellationToken cancellationToken)
	{
		await process.GracefulWaitForExitAsync(cancellationToken).ConfigureAwait(false);
	}

	public void KillProcess(Process process)
	{
		process.Kill();
	}

	/// <summary>
	/// Connects to the Tor SOCKS5 proxy and starts the handshaking process to find out if Tor is up and ready.
	/// </summary>
	public virtual async Task<bool> IsTorRunningAsync(EndPoint socksEndpoint, CancellationToken cancellationToken)
	{
		if (!socksEndpoint.TryGetHostAndPort(out var host, out var port))
		{
			throw new InvalidOperationException("The Tor SOCKS5 endpoint is not supported.");
		}

		try
		{
			using var tcp = new TcpClient(socksEndpoint.AddressFamily);
			await tcp.ConnectAsync(host, port.Value, cancellationToken).ConfigureAwait(false);

			var networkStream = tcp.GetStream();

			await networkStream.WriteAsync(NoAuthHandshakeMsg, cancellationToken).ConfigureAwait(false);

			var response = new byte[2];
			await networkStream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
			bool isTorRunning = response is [0x05, 0x00];

			_eventBus.Publish(new TorConnectionStateChanged(isTorRunning));
			return isTorRunning;
		}
		catch (SocketException socketException) when (socketException.SocketErrorCode == SocketError.TimedOut && cancellationToken.IsCancellationRequested)
		{
			// The expectation is that if the conditions are met that the user really canceled the operation. Rarely it might not be true but it's a reasonable assumption.
			throw new OperationCanceledException("The operation was canceled.", socketException);
		}
		catch (SocketException ex)
		{
			// Any other socket error means Tor is not usable right now.
			Logger.LogInfo($"Failed to connect to {socksEndpoint}. Socket error code was {ex.SocketErrorCode} ({ex.ErrorCode}): {ex.Message}");
			_eventBus.Publish(new TorConnectionStateChanged(false));
			return false;
		}
		catch (IOException ex)
		{
			// Handles reading and writing operations of the network stream.
			Logger.LogInfo($"SOCKS5 handshake with {socksEndpoint} failed: {ex.Message}");
			_eventBus.Publish(new TorConnectionStateChanged(false));
			return false;
		}
	}

	/// <summary>
	/// Ensure <paramref name="process"/> is actually running.
	/// </summary>
	public virtual async Task<bool> EnsureRunningAsync(Process process, EndPoint socksEndpoint, CancellationToken token)
	{
		int i = 0;
		while (true)
		{
			i++;

			bool isRunning = await IsTorRunningAsync(socksEndpoint, token).ConfigureAwait(false);

			if (isRunning)
			{
				return true;
			}

			if (process.HasExited)
			{
				Logger.LogError("Tor process failed to start!");
				return false;
			}

			const int MaxAttempts = 25;

			if (i >= MaxAttempts)
			{
				Logger.LogError($"All {MaxAttempts} attempts to connect to Tor failed.");
				return false;
			}

			// Wait 250 milliseconds between attempts.
			await Task.Delay(250, token).ConfigureAwait(false);
		}
	}

	public virtual Process[] GetTorProcesses()
	{
		return Process.GetProcessesByName(TorSettings.TorBinaryFileName);
	}

	public bool KillBundledRunningTorProcesses(TorSettings settings, out Process[] torProcesses)
	{
		// If Tor was already started, we don't have Tor process ID (pid), so it's harder to kill it.
		torProcesses = GetTorProcesses();
		bool killAttempt = false;
		foreach (Process torProcess in torProcesses)
		{
			try
			{
				// Kill only the Tor processes that are running from the bundled folder. Not system Tor processes.
				// This throws if we can't access MainModule of an elevated process from a non elevated one.
				if (torProcess.MainModule?.FileName == settings.TorBinaryFilePath)
				{
					Logger.LogInfo("Kill running Tor process to restart it again.");
					killAttempt = true;
					KillProcess(torProcess);
				}
			}
			catch
			{
			}
		}

		return killAttempt;
	}

	/// <summary>
	/// Connects to Tor control using a TCP client or throws <see cref="TorControlException"/>.
	/// </summary>
	/// <exception cref="TorControlException">When authentication fails for some reason.</exception>
	/// <seealso href="https://github.com/torproject/torspec/blob/main/control-spec.txt">This method follows instructions in 3.23. TAKEOWNERSHIP.</seealso>
	public virtual async Task<TorControlClient> InitTorControlAsync(TorSettings settings, CancellationToken cancellationToken)
	{
		// If the cookie file does not exist, we know our Tor starting procedure is corrupted somehow. Best to start from scratch.
		if (!File.Exists(settings.CookieAuthFilePath))
		{
			throw new TorControlException("Cookie file does not exist.");
		}

		// Get cookie.
		string cookieString = Convert.ToHexString(File.ReadAllBytes(settings.CookieAuthFilePath));

		// Authenticate.
		var factory = new TorControlClientFactory(RandomnessProviders.Secure);
		var client = await factory.ConnectAndAuthenticateAsync(settings.ControlEndpoint, cookieString, cancellationToken).ConfigureAwait(false);

		if (settings.TerminateOnExit)
		{
			// This is necessary for the scenario when Tor was started by a previous WW instance with TerminateTorOnExit=false configuration option.
			TorControlReply takeReply = await client.TakeOwnershipAsync(cancellationToken).ConfigureAwait(false);

			if (!takeReply)
			{
				throw new TorControlException($"Failed to take ownership of the Tor instance. Reply: '{takeReply}'.");
			}

			TorControlReply resetReply = await client.ResetOwningControllerProcessConfAsync(cancellationToken).ConfigureAwait(false);

			if (!resetReply)
			{
				throw new TorControlException($"Failed to reset __OwningControllerProcess. Reply: '{resetReply}'.");
			}
		}

		await WaitForBootstrapAsync(client, cancellationToken).ConfigureAwait(false);

		return client;
	}

	/// <exception cref="TorControlException">When bootstrap does not finish for some reason.</exception>
	public static async Task WaitForBootstrapAsync(TorControlClient client, CancellationToken cancellationToken)
	{
		// Subscribe to Tor's STATUS_CLIENT events to get bootstrapping updates.
		IReadOnlyList<string> eventNames = ["STATUS_CLIENT"];
		using var subscription = await client.SubscribeEventsAsync(eventNames, cancellationToken).ConfigureAwait(false);

		// Tor may have finished bootstrapping before we subscribed.
		var phase = await client.GetBootstrapPhaseAsync(cancellationToken).ConfigureAwait(false);
		if (phase.IsBootstrapped)
		{
			return;
		}

		// Precaution in case Tor is not bootstrapping at all. It's not really expected to happen.
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(BootstrapTimeoutMs);

		try
		{
			await foreach (var @event in subscription.ReadAllAsync(timeoutCts.Token).ConfigureAwait(false))
			{
				try
				{
					var asyncEvent = AsyncEventParser.Parse(@event);

					if (asyncEvent is BootstrapStatusEvent bootstrapEvent)
					{
						if (bootstrapEvent.IsBootstrapped)
						{
							return;
						}

						// Do not flood logs.
						bool log = bootstrapEvent.Recommendation == "warn" && (bootstrapEvent.Count is null || bootstrapEvent.Count < 3 || bootstrapEvent.Count % 5 == 0);

						if (log)
						{
							string reason = $"Reason={bootstrapEvent.Reason ?? "N/A"}";
							string hostAddress = bootstrapEvent.HostAddr is null ? "" : $", HostAddress={bootstrapEvent.HostAddr}";
							string count = bootstrapEvent.Count is null ? "" : $", Count={bootstrapEvent.Count}";
							string msg = $"Bootstrap warning at {bootstrapEvent.Progress}% ({bootstrapEvent.Tag}): {bootstrapEvent.Warning} ({reason}{hostAddress}{count}).";
							Logger.LogWarning(msg);
						}
					}
				}
				catch (Exception ex)
				{
					// Ignore events we cannot parse.
					Logger.LogTrace($"Failed to parse Tor control event: {@event}.", ex);
				}
			}

			throw new TorControlException("Tor control connection closed before Tor bootstrapped.");
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			Logger.LogWarning($"Tor has not bootstrapped in {BootstrapTimeoutMs / 1000.0} seconds. Not waiting any longer.");
			return;
		}
	}
}
