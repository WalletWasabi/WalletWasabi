using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BundledApps;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor;

public class ProcessManager
{
	public ProcessManager(TorSettings settings, EventBus eventBus)
	{
		_settings = settings;
		_eventBus = eventBus;
	}

	private readonly TorSettings _settings;
	private readonly EventBus _eventBus;

	/// <param name="arguments">Command line arguments to start Tor OS process with.</param>
	public virtual ProcessAsync StartProcess(string arguments)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = _settings.TorBinaryFilePath,
			Arguments = arguments,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			WorkingDirectory = _settings.TorBinaryDir
		};

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var env = startInfo.EnvironmentVariables;

			env["LD_LIBRARY_PATH"] = !env.ContainsKey("LD_LIBRARY_PATH") || string.IsNullOrEmpty(env["LD_LIBRARY_PATH"])
				? _settings.TorBinaryDir
				: _settings.TorBinaryDir + Path.PathSeparator + env["LD_LIBRARY_PATH"];

			Logger.LogDebug($"Environment variable 'LD_LIBRARY_PATH' set to: '{env["LD_LIBRARY_PATH"]}'.");
		}

		Logger.LogInfo(_settings.IsCustomTorFolder ? $"Starting Tor process in folder '{_settings.TorBinaryDir}'…" : "Starting Tor process…");
		ProcessAsync process = new(startInfo);
		process.Start();

		return process;
	}

	public virtual async Task WaitForProcessExitAsync(ProcessAsync process, CancellationToken cancellationToken)
	{
		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual void KillProcess(Process process)
	{
		process.Kill();
	}

	public virtual void KillProcess(ProcessAsync process)
	{
		process.Kill();
	}

	public virtual async Task<bool> IsTorRunningAsync(CancellationToken cancellationToken)
	{
		// This function connects to the Tor Socks5 proxy and starts the handshaking process
		if (!_settings.SocksEndpoint.TryGetHostAndPort(out var host, out var port))
		{
			throw new InvalidOperationException("The Tor socks5 endpoint is not supported.");
		}

		try
		{
			using var tcp = new TcpClient(_settings.SocksEndpoint.AddressFamily);
			await tcp.ConnectAsync(host, port.Value, cancellationToken).ConfigureAwait(false);
			byte[] msg =
			[
				0x05, // Version
				0x01, // One method
				0x00, // No authentication
			];
			var response = new byte[2];
			await tcp.Client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
			var read = await tcp.Client.ReceiveAsync(response, cancellationToken).ConfigureAwait(false);
			var isTorRunning = read == 2 && response is [0x05, 0x00];

			_eventBus.Publish(new TorConnectionStateChanged(isTorRunning));
			return isTorRunning;
		}
		catch (SocketException socketException) when (socketException.SocketErrorCode == SocketError.TimedOut && cancellationToken.IsCancellationRequested)
		{
			// The expectation is that if the conditions are met that the user really canceled the operation. Rarely it might not be true but it's a reasonable assumption.
			throw new OperationCanceledException("The operation was canceled.", socketException);
		}
		catch (SocketException socketException) when (socketException.SocketErrorCode == SocketError.ConnectionRefused)
		{
			_eventBus.Publish(new TorConnectionStateChanged(false));
			return false;
		}
	}

	/// <summary>Ensure <paramref name="process"/> is actually running.</summary>
	public virtual async Task<bool> EnsureRunningAsync(ProcessAsync process, CancellationToken token)
	{
		int i = 0;
		while (true)
		{
			i++;

			bool isRunning = await IsTorRunningAsync(token).ConfigureAwait(false);

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

	/// <summary>Connects to Tor control using a TCP client or throws <see cref="TorControlException"/>.</summary>
	/// <exception cref="TorControlException">When authentication fails for some reason.</exception>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">This method follows instructions in 3.23. TAKEOWNERSHIP.</seealso>
	public virtual async Task<TorControlClient> InitTorControlAsync(CancellationToken token)
	{
		// If the cookie file does not exist, we know our Tor starting procedure is corrupted somehow. Best to start from scratch.
		if (!File.Exists(_settings.CookieAuthFilePath))
		{
			throw new TorControlException("Cookie file does not exist.");
		}

		// Get cookie.
		string cookieString = Convert.ToHexString(File.ReadAllBytes(_settings.CookieAuthFilePath));

		// Authenticate.
		TorControlClientFactory factory = new();
		TorControlClient client = await factory.ConnectAndAuthenticateAsync(_settings.ControlEndpoint, cookieString, token).ConfigureAwait(false);

		if (_settings.TerminateOnExit)
		{
			// This is necessary for the scenario when Tor was started by a previous WW instance with TerminateTorOnExit=false configuration option.
			TorControlReply takeReply = await client.TakeOwnershipAsync(token).ConfigureAwait(false);

			if (!takeReply)
			{
				throw new TorControlException($"Failed to take ownership of the Tor instance. Reply: '{takeReply}'.");
			}

			TorControlReply resetReply = await client.ResetOwningControllerProcessConfAsync(token).ConfigureAwait(false);

			if (!resetReply)
			{
				throw new TorControlException($"Failed to reset __OwningControllerProcess. Reply: '{resetReply}'.");
			}
		}

		return client;
	}
}
