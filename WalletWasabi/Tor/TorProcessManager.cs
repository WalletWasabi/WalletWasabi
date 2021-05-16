using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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
	/// <summary>
	/// Manages lifetime of Tor process.
	/// </summary>
	/// <seealso href="https://2019.www.torproject.org/docs/tor-manual.html.en"/>
	public class TorProcessManager
	{
		private bool _disposed = false;

		public TorProcessManager(TorSettings settings, EndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = torSocks5EndPoint;
			TorProcess = null;
			TorControlClient = null;
			Settings = settings;
			TcpConnectionFactory = new(torSocks5EndPoint);
		}

		private EndPoint TorSocks5EndPoint { get; }
		private ProcessAsync? TorProcess { get; set; }
		private TorSettings Settings { get; }
		private TorTcpConnectionFactory TcpConnectionFactory { get; }

		private TorControlClient? TorControlClient { get; set; }

		/// <summary>
		/// Starts Tor process if it is not running already.
		/// </summary>
		/// <exception cref="OperationCanceledException"/>
		public async Task<bool> StartAsync(CancellationToken token = default)
		{
			ThrowIfDisposed();

			try
			{
				// Is Tor already running? Either our Tor process from previous Wasabi Wallet run or possibly user's own Tor.
				bool isAlreadyRunning = await TcpConnectionFactory.IsTorRunningAsync().ConfigureAwait(false);

				if (isAlreadyRunning)
				{
					string msg = TorSocks5EndPoint is IPEndPoint endpoint
						? $"Tor is already running on {endpoint.Address}:{endpoint.Port}."
						: "Tor is already running.";
					Logger.LogInfo(msg);

					TorControlClient = await InitTorControlOrThrowAsync(token).ConfigureAwait(false);
					return true;
				}

				string torArguments = Settings.GetCmdArguments(TorSocks5EndPoint);

				ProcessStartInfo startInfo = new()
				{
					FileName = Settings.TorBinaryFilePath,
					Arguments = torArguments,
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

				TorProcess = new(startInfo);

				Logger.LogInfo($"Starting Tor process ...");
				TorProcess.Start();

				// Ensure it's running.
				int i = 0;
				while (true)
				{
					i++;

					bool isRunning = await TcpConnectionFactory.IsTorRunningAsync().ConfigureAwait(false);

					if (isRunning)
					{
						break;
					}

					if (TorProcess.HasExited)
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

				Logger.LogInfo("Tor is running.");
				TorControlClient = await InitTorControlOrThrowAsync(token).ConfigureAwait(false);

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

			return false;
		}

		/// <summary>
		/// Connects to Tor control using a TCP client or throws <see cref="TorControlException"/>.
		/// </summary>
		/// <exception cref="TorControlException">When authentication fails for some reason.</exception>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">This method follows instructions in 3.23. TAKEOWNERSHIP.</seealso>
		private async Task<TorControlClient> InitTorControlOrThrowAsync(CancellationToken token = default)
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

		public async Task StopAsync()
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
				torControlClient.Dispose();
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
