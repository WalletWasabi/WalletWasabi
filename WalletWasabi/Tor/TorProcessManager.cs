using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor
{
	/// <summary>Manages lifetime of Tor process.</summary>
	/// <seealso href="https://2019.www.torproject.org/docs/tor-manual.html.en"/>
	public class TorProcessManager : IAsyncDisposable
	{
		private bool _disposed = false;

		public TorProcessManager(TorSettings settings) :
			this(settings, new(settings.SocksEndpoint))
		{
		}

		/// <summary>For tests.</summary>
		internal TorProcessManager(TorSettings settings, TorTcpConnectionFactory tcpConnectionFactory)
		{
			TorProcess = null;
			TorControlClient = null;
			Settings = settings;
			TcpConnectionFactory = tcpConnectionFactory;
		}

		private ProcessAsync? TorProcess { get; set; }
		private TorSettings Settings { get; }
		private TorTcpConnectionFactory TcpConnectionFactory { get; }

		private TorControlClient? TorControlClient { get; set; }

		/// <summary>Starts Tor process if it is not running already.</summary>
		/// <exception cref="OperationCanceledException"/>
		public async Task<bool> StartAsync(CancellationToken token = default)
		{
			ThrowIfDisposed();

			ProcessAsync? process = null;
			TorControlClient? controlClient = null;

			try
			{
				// Is Tor already running? Either our Tor process from previous Wasabi Wallet run or possibly user's own Tor.
				bool isAlreadyRunning = await TcpConnectionFactory.IsTorRunningAsync().ConfigureAwait(false);

				if (isAlreadyRunning)
				{
					Logger.LogInfo($"Tor is already running on {Settings.SocksEndpoint.Address}:{Settings.SocksEndpoint.Port}.");
					TorControlClient = await InitTorControlAsync(token).ConfigureAwait(false);
					return true;
				}

				string arguments = Settings.GetCmdArguments();
				process = StartProcess(arguments);

				bool isRunning = await EnsureRunningAsync(process, token).ConfigureAwait(false);

				if (!isRunning)
				{
					return false;
				}

				controlClient = await InitTorControlAsync(token).ConfigureAwait(false);
				Logger.LogInfo("Tor is running.");

				// Only now we know that Tor process is fully started.
				TorProcess = process;
				TorControlClient = controlClient;
				controlClient = null;
				process = null;

				return true;
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogDebug("User canceled operation.", ex);
				throw;
			}
			catch (Exception ex)
			{
				Logger.LogError("Could not automatically start Tor. Try running Tor manually.", ex);
			}
			finally
			{
				if (controlClient is not null)
				{
					await controlClient.DisposeAsync().ConfigureAwait(false);
				}

				process?.Dispose();
			}

			return false;
		}

		/// <summary>Ensure <paramref name="process"/> is actually running.</summary>
		internal virtual async Task<bool> EnsureRunningAsync(ProcessAsync process, CancellationToken token)
		{
			int i = 0;
			while (true)
			{
				i++;

				bool isRunning = await TcpConnectionFactory.IsTorRunningAsync().ConfigureAwait(false);

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

			Logger.LogInfo("Starting Tor process ...");
			ProcessAsync process = new(startInfo);
			process.Start();

			return process;
		}

		/// <summary>Connects to Tor control using a TCP client or throws <see cref="TorControlException"/>.</summary>
		/// <exception cref="TorControlException">When authentication fails for some reason.</exception>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">This method follows instructions in 3.23. TAKEOWNERSHIP.</seealso>
		internal virtual async Task<TorControlClient> InitTorControlAsync(CancellationToken token = default)
		{
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
			_disposed = true;

			if (TorControlClient is TorControlClient torControlClient)
			{
				// Even though terminating the TCP connection with Tor would shut down Tor,
				// the spec is quite clear:
				// > As of Tor 0.2.5.2-alpha, Tor does not wait a while for circuits to
				// > close when shutting down because of an exiting controller. If you
				// > want to ensure a clean shutdown--and you should!--then send "SIGNAL
				// > SHUTDOWN" and wait for the Tor process to close.)
				if (Settings.TerminateOnExit)
				{
					await torControlClient.SignalShutdownAsync().ConfigureAwait(false);
				}

				// Leads to Tor termination because we sent TAKEOWNERSHIP command.
				await torControlClient.DisposeAsync().ConfigureAwait(false);
			}

			// Dispose Tor process resources (does not stop/kill Tor process).
			TorProcess?.Dispose();
		}

		private void ThrowIfDisposed()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}
		}
	}
}
