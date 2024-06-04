using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor;

/// <summary>Manages lifetime of Tor process.</summary>
/// <seealso href="https://2019.www.torproject.org/docs/tor-manual.html.en"/>
public class TorProcessManager : IAsyncDisposable
{
	internal const string TorProcessStartedByDifferentUser = "Tor was started by another user and we can't use it nor kill it.";

	/// <summary>Task completion source returning a cancellation token which is canceled when Tor process is terminated.</summary>
	private volatile TaskCompletionSource<(CancellationToken, TorControlClient?)> _tcs = new();

	public TorProcessManager(TorSettings settings) :
		this(settings, new(settings.SocksEndpoint))
	{
	}

	/// <summary>For tests.</summary>
	internal TorProcessManager(TorSettings settings, TorTcpConnectionFactory tcpConnectionFactory)
	{
		TorProcess = null;
		TorControlClient = null;
		LoopCts = new();
		LoopTask = null;
		Settings = settings;
		TcpConnectionFactory = tcpConnectionFactory;
	}

	/// <summary>Guards <see cref="TorProcess"/> and <see cref="TorControlClient"/>.</summary>
	private object StateLock { get; } = new();

	private Task? LoopTask { get; set; }

	/// <summary>To stop the loop that keeps starting Tor process.</summary>
	private CancellationTokenSource LoopCts { get; }

	private TorSettings Settings { get; }
	private TorTcpConnectionFactory TcpConnectionFactory { get; }

	/// <remarks>
	/// Only set if <see cref="TorMode.Enabled"/> is not on.
	/// <para>Guarded by <see cref="StateLock"/>.</para>
	/// </remarks>
	private ProcessAsync? TorProcess { get; set; }

	/// <remarks>
	/// Only set if <see cref="TorMode.Enabled"/> is not on.
	/// <para>Guarded by <see cref="StateLock"/>.</para>
	/// </remarks>
	private TorControlClient? TorControlClient { get; set; }

	/// <inheritdoc cref="StartAsync(int, CancellationToken)"/>
	public Task<(CancellationToken, TorControlClient?)> StartAsync(CancellationToken cancellationToken)
	{
		return StartAsync(attempts: 1, cancellationToken);
	}

	/// <summary>Starts loop which makes sure that Tor process is started.</summary>
	/// <param name="cancellationToken">Application lifetime cancellation token.</param>
	/// <returns>Cancellation token which is canceled once Tor process terminates (either forcefully or gracefully).</returns>
	/// <remarks>This method must be called exactly once.</remarks>
	/// <exception cref="OperationCanceledException">When the operation is cancelled by the user.</exception>
	/// <exception cref="InvalidOperationException">When all attempts are tried without success.</exception>
	public async Task<(CancellationToken, TorControlClient?)> StartAsync(int attempts, CancellationToken cancellationToken)
	{
		LoopTask = RestartingLoopAsync(cancellationToken);

		string operation = Settings.TorMode == TorMode.Enabled ? "start" : "connect to";

		for (int i = 0; i < attempts; i++)
		{
			try
			{
				Logger.LogDebug($"Attempt #{i + 1} to {operation} Tor.");
				return await WaitForNextAttemptAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
			}
		}

		throw new InvalidOperationException($"No attempt to {operation} Tor was successful.");
	}

	/// <summary>Waits until Tor process is fully started or until it is stopped for some reason.</summary>
	/// <returns>Cancellation token which is canceled once Tor process terminates or once <paramref name="cancellationToken"/> is canceled.</returns>
	/// <remarks>This is useful to set up Tor control monitors that need to be restarted once Tor process is started again.</remarks>
	public Task<(CancellationToken, TorControlClient?)> WaitForNextAttemptAsync(CancellationToken cancellationToken)
	{
		return _tcs.Task.WaitAsync(cancellationToken);
	}

	/// <summary>Keeps starting Tor OS process.</summary>
	/// <param name="globalCancellationToken">Application lifetime cancellation token.</param>
	private async Task RestartingLoopAsync(CancellationToken globalCancellationToken)
	{
		if (Settings.TorMode == TorMode.EnabledOnlyRunning)
		{
			await RestartingLoopForRunningTorAsync(globalCancellationToken).ConfigureAwait(false);
		}
		else
		{
			await RestartingLoopForBundledTorAsync(globalCancellationToken).ConfigureAwait(false);
		}
	}

	private async Task RestartingLoopForRunningTorAsync(CancellationToken globalCancellationToken)
	{
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCancellationToken, LoopCts.Token);
		CancellationToken cancellationToken = linkedCts.Token;

		bool detectedTorState = false;
		CancellationTokenSource torStoppedCts = new();

		while (!cancellationToken.IsCancellationRequested)
		{
			bool isTorRunning = await TcpConnectionFactory.IsTorRunningAsync(cancellationToken).ConfigureAwait(false);

			if (detectedTorState && isTorRunning) // Case: Still running.
			{
				// No-op.
			}
			else if (!detectedTorState && isTorRunning) // Case: Started running.
			{
				Logger.LogDebug("Connection to Tor SOCKS5 is established.");
				_tcs.SetResult((torStoppedCts.Token, null));
			}
			else if (!isTorRunning) // Case: Stopped running, or still not running.
			{
				if (detectedTorState)
				{
					Logger.LogDebug("Connection to Tor SOCKS5 was lost.");
				}

				bool willWaitForTorRestart = !cancellationToken.IsCancellationRequested;
				OnTorStop(willWaitForTorRestart, exception: null, torStoppedCts, globalCancellationToken);
				torStoppedCts = new();
			}

			detectedTorState = isTorRunning;

			// We don't use Tor Control client with an already running Tor, so we just use a simple sleep mechanism.
			await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

			if (cancellationToken.IsCancellationRequested)
			{
				Logger.LogDebug("User canceled operation.");
				break;
			}
		}

		torStoppedCts.Dispose();
	}

	/// <summary>
	/// Keeps starting Tor OS process.
	/// </summary>
	private async Task RestartingLoopForBundledTorAsync(CancellationToken globalCancellationToken)
	{
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCancellationToken, LoopCts.Token);
		CancellationToken cancellationToken = linkedCts.Token;

		while (!cancellationToken.IsCancellationRequested)
		{
			ProcessAsync? process = null;
			TorControlClient? controlClient = null;
			Exception? exception = null;
			bool setNewTcs = true;

			// Use CancellationTokenSource to signal that Tor process terminated.
			using CancellationTokenSource cts = new();

			try
			{
				// Is Tor already running? Either our Tor process from previous Wasabi Wallet run or possibly user's own Tor.
				bool isAlreadyRunning = await TcpConnectionFactory.IsTorRunningAsync(cancellationToken).ConfigureAwait(false);

				if (isAlreadyRunning)
				{
					Logger.LogInfo($"Tor is already running on {Settings.SocksEndpoint}");
					controlClient = await InitTorControlAsync(cancellationToken).ConfigureAwait(false);

					// Tor process can crash even between these two commands too.
					int processId = await controlClient.GetTorProcessIdAsync(cancellationToken).ConfigureAwait(false);
					process = new ProcessAsync(Process.GetProcessById(processId));

					try
					{
						// Note: This is a workaround how to check whether we have sufficient permissions for the process.
						// Especially, we want to make sure that Tor is running under our user and not a different one.
						// Example situation: Tor is run under admin account but then the app is run under a non-privileged account.
						nint _ = process.Handle;
					}
					catch (Exception ex)
					{
						throw new NotSupportedException(TorProcessStartedByDifferentUser, ex);
					}

					TorControlReply clientTransportPluginReply = await controlClient.GetConfAsync(keyword: "ClientTransportPlugin", cancellationToken).ConfigureAwait(false);
					if (!clientTransportPluginReply.Success)
					{
						throw new InvalidOperationException("Tor control failed to report the current transport plugin.");
					}

					// Check if the bridges in the running Tor instance are the same as user requested.
					TorControlReply bridgeReply = await controlClient.GetConfAsync(keyword: "Bridge", cancellationToken).ConfigureAwait(false);
					if (!bridgeReply.Success)
					{
						throw new InvalidOperationException("Tor control failed to report active bridges.");
					}

					// Compare as two unordered sets.
					string[] currentBridges = bridgeReply.ResponseLines.Where(x => x != "Bridge").Select(x => x.Split('=', 2)[1]).Order().ToArray();
					bool areBridgesAsRequired = currentBridges.SequenceEqual(Settings.Bridges.Order());

					if (!areBridgesAsRequired)
					{
						Logger.LogInfo("Tor bridges of the running Tor instance are different than required. Restarting Tor.");
						await controlClient.SignalShutdownAsync(cancellationToken).ConfigureAwait(false);
						continue;
					}
				}
				else
				{
					string arguments = Settings.GetCmdArguments();
					Logger.LogTrace($"Starting Tor with arguments: {arguments}");

					process = StartProcess(arguments);

					bool isRunning = await EnsureRunningAsync(process, cancellationToken).ConfigureAwait(false);

					if (!isRunning)
					{
						Logger.LogTrace("Failed to start Tor process. Trying again.");
						continue;
					}

					controlClient = await InitTorControlAsync(cancellationToken).ConfigureAwait(false);
				}

				Logger.LogInfo("Tor is running.");

				// Only now we know that Tor process is fully started.
				lock (StateLock)
				{
					TorProcess = process;
					TorControlClient = controlClient;

					_tcs.SetResult((cts.Token, controlClient));
				}

				await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

				Logger.LogDebug("Tor process exited.");
			}
			catch (OperationCanceledException)
			{
				Logger.LogDebug("User canceled operation.");
				setNewTcs = false;
				break;
			}
			catch (TorControlException ex)
			{
				Logger.LogDebug("Tor control failed to initialize.", ex);

				// If Tor control fails to initialize, we want to try to start Tor again and initialize Tor control again.
				if (process is not null)
				{
					Logger.LogDebug("Attempt to kill the running Tor process.");
					process.Kill();
				}
				else
				{
					// If Tor was already started, we don't have Tor process ID (pid), so it's harder to kill it.
					Process[] torProcesses = GetTorProcesses();

					bool killAttempt = false;

					foreach (Process torProcess in torProcesses)
					{
						try
						{
							// This throws if we can't access MainModule of an elevated process from a non elevated one.
							if (torProcess.MainModule?.FileName == Settings.TorBinaryFilePath)
							{
								Logger.LogInfo("Kill running Tor process to restart it again.");
								killAttempt = true;
								torProcess.Kill();
							}
						}
						catch
						{
						}
					}

					// Tor was started by another user and we can't kill it.
					if (torProcesses.Length == 0 || !killAttempt)
					{
						Logger.LogDebug("Failed to find the Tor process in the list of processes.");
						setNewTcs = false;
						exception = new NotSupportedException(TorProcessStartedByDifferentUser, ex);
						throw exception;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Unexpected problem in starting Tor.", ex);
				setNewTcs = false;
				exception = ex;
				throw;
			}
			finally
			{
				OnTorStop(setNewTcs, exception, cts, globalCancellationToken);

				if (controlClient is not null)
				{
					await controlClient.DisposeAsync().ConfigureAwait(false);
				}

				process?.Dispose();

				lock (StateLock)
				{
					TorProcess = null;
					TorControlClient = null;
				}
			}
		}
	}

	private void OnTorStop(bool willWaitForTorRestart, Exception? exception, CancellationTokenSource torStoppedCts, CancellationToken globalCancellationToken)
	{
		TaskCompletionSource<(CancellationToken, TorControlClient?)> originalTcs = _tcs;

		if (willWaitForTorRestart)
		{
			// (1) and (2) must be in this order. Otherwise, there is a race condition risk of getting invalid CT by clients.
			TaskCompletionSource<(CancellationToken, TorControlClient?)> newTcs = new();
			originalTcs = Interlocked.Exchange(ref _tcs, newTcs); // (1)
		}

		torStoppedCts.Cancel(); // (2)

		if (exception is not null)
		{
			originalTcs.TrySetException(exception);
		}
		else
		{
			originalTcs.TrySetCanceled(globalCancellationToken);
		}

		torStoppedCts.Dispose();
	}

	internal virtual Process[] GetTorProcesses()
	{
		return Process.GetProcessesByName(TorSettings.TorBinaryFileName);
	}

	/// <summary>Ensure <paramref name="process"/> is actually running.</summary>
	internal virtual async Task<bool> EnsureRunningAsync(ProcessAsync process, CancellationToken token)
	{
		int i = 0;
		while (true)
		{
			i++;

			bool isRunning = await TcpConnectionFactory.IsTorRunningAsync(token).ConfigureAwait(false);

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

	/// <param name="arguments">Command line arguments to start Tor OS process with.</param>
	internal virtual ProcessAsync StartProcess(string arguments)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = Settings.TorBinaryFilePath,
			Arguments = arguments,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			WorkingDirectory = Settings.TorBinaryDir
		};

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var env = startInfo.EnvironmentVariables;

			env["LD_LIBRARY_PATH"] = !env.ContainsKey("LD_LIBRARY_PATH") || string.IsNullOrEmpty(env["LD_LIBRARY_PATH"])
				? Settings.TorBinaryDir
				: Settings.TorBinaryDir + Path.PathSeparator + env["LD_LIBRARY_PATH"];

			Logger.LogDebug($"Environment variable 'LD_LIBRARY_PATH' set to: '{env["LD_LIBRARY_PATH"]}'.");
		}

		Logger.LogInfo(Settings.IsCustomTorFolder ? $"Starting Tor process in folder '{Settings.TorBinaryDir}'…" : "Starting Tor process…");
		ProcessAsync process = new(startInfo);
		process.Start();

		return process;
	}

	/// <summary>Connects to Tor control using a TCP client or throws <see cref="TorControlException"/>.</summary>
	/// <exception cref="TorControlException">When authentication fails for some reason.</exception>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">This method follows instructions in 3.23. TAKEOWNERSHIP.</seealso>
	internal virtual async Task<TorControlClient> InitTorControlAsync(CancellationToken token)
	{
		// If the cookie file does not exist, we know our Tor starting procedure is corrupted somehow. Best to start from scratch.
		if (!File.Exists(Settings.CookieAuthFilePath))
		{
			throw new TorControlException("Cookie file does not exist.");
		}

		// Get cookie.
		string cookieString = ByteHelpers.ToHex(File.ReadAllBytes(Settings.CookieAuthFilePath));

		// Authenticate.
		TorControlClientFactory factory = new();
		TorControlClient client = await factory.ConnectAndAuthenticateAsync(Settings.ControlEndpoint, cookieString, token).ConfigureAwait(false);

		if (Settings.TerminateOnExit)
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

	public async ValueTask DisposeAsync()
	{
		LoopCts.Cancel();

		if (LoopTask is Task t)
		{
			if (!t.IsFaulted)
			{
				await t.ConfigureAwait(false);
			}
		}

		LoopCts.Dispose();

		ProcessAsync? process;
		TorControlClient? torControlClient;

		lock (StateLock)
		{
			process = TorProcess;
			torControlClient = TorControlClient;
		}

		if (torControlClient is not null)
		{
			// Even though terminating the TCP connection with Tor would shut down Tor,
			// the spec is quite clear:
			// > As of Tor 0.2.5.2-alpha, Tor does not wait a while for circuits to
			// > close when shutting down because of an exiting controller. If you
			// > want to ensure a clean shutdown--and you should!--then send "SIGNAL
			// > SHUTDOWN" and wait for the Tor process to close.)
			if (Settings.TerminateOnExit)
			{
				await torControlClient.SignalShutdownAsync(CancellationToken.None).ConfigureAwait(false);
			}

			// Leads to Tor termination because we sent TAKEOWNERSHIP command.
			await torControlClient.DisposeAsync().ConfigureAwait(false);
		}

		// Dispose Tor process resources (does not stop/kill Tor process).
		process?.Dispose();
	}
}
