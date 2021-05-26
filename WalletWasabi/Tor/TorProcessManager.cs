using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
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

		public TorProcessManager(TorSettings settings)
		{
			Settings = settings;
			TcpConnectionFactory = new(settings.SocksEndpoint);
		}

		private ProcessAsync? TorProcess { get; set; }
		private TorSettings Settings { get; }
		private TorTcpConnectionFactory TcpConnectionFactory { get; }
		private TorControlClient? TorControlClient { get; set; }

		/// <summary>
		/// Starts Tor process if it is not running already.
		/// </summary>
		/// <exception cref="OperationCanceledException"/>
		/// <exception cref="TorControlException">When Tor is started/running but Tor control failed to be initialized. This is unrecoverable situation.</exception>
		public async Task<bool> StartAsync(CancellationToken token = default)
		{
			ThrowIfDisposed();

			ProcessAsync? process = null;
			TorControlClient? controlClient = null;

			try
			{
				// Option 1: Tor is already running. Just connect to the connect Tor control port.
				bool isAlreadyRunning = await TcpConnectionFactory.IsTorRunningAsync().ConfigureAwait(false);

				if (isAlreadyRunning)
				{
					Logger.LogInfo($"Tor is already running on {Settings.SocksEndpoint.Address}:{Settings.SocksEndpoint.Port}.");
					TorControlClient = await InitTorControlAsync(Settings.GetTorControlPort(), token).ConfigureAwait(false);
					return true;
				}

				// Option 2: Tor is not running. Start it with "--ControlPort auto", store a control port in a file and connect to Tor control using that port.
				string torArguments = Settings.GetCmdArguments();

				string watchedFolder = Path.GetDirectoryName(Settings.ControlPortRandomFilePath)!;
				string watchedFile = Path.GetFileName(Settings.ControlPortRandomFilePath);

				using (FileSystemWatcher fsWatcher = new(path: watchedFolder, watchedFile))
				{
					fsWatcher.EnableRaisingEvents = true;
					Task<WaitForChangedResult> watchTask = Task.Run(() =>
						fsWatcher.WaitForChanged(WatcherChangeTypes.Created | WatcherChangeTypes.Renamed, timeout: 5_000));

					process = StartProcess(torArguments);

					bool isRunning = await EnsureRunningAsync(process, token).ConfigureAwait(false);

					if (!isRunning)
					{
						return false;
					}

					WaitForChangedResult watcherResult = await watchTask.ConfigureAwait(false);

					// Either FileSystemWatcher returned a result or it did not but the file exists anyway both is fine for us.
					// When neither is true, we are in trouble.
					if (watcherResult.TimedOut && !File.Exists(Settings.ControlPortRandomFilePath))
					{
						throw new TorControlException("Tor did not created file containing control port.");
					}

					int portNumber = ProcessControlPortFileCreatedByTor(Settings.ControlPortRandomFilePath);
					Logger.LogDebug($"Tor control is running on port {portNumber}.");

					controlClient = await InitTorControlAsync(portNumber, token).ConfigureAwait(false);
				}

				Logger.LogInfo("Tor is running.");

				// Only now we know that Tor process is fully correctly started.
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
			catch (TorControlException ex)
			{
				Logger.LogError("Failed to initialize Tor control for Tor process. Probably unrecoverable.", ex);
			}
			catch (Exception ex)
			{
				Logger.LogError("Could not automatically start Tor. Try running Tor manually.", ex);
			}
			finally
			{
				controlClient?.Dispose();
				process?.Dispose();
			}

			return false;
		}

		private ProcessAsync StartProcess(string torArguments)
		{
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

			Logger.LogInfo("Starting Tor process ...");
			ProcessAsync torProcess = new(startInfo);
			torProcess.Start();

			return torProcess;
		}

		private async Task<bool> EnsureRunningAsync(ProcessAsync process, CancellationToken cancellationToken)
		{
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
				await Task.Delay(250, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}

		/// <param name="tempPath">Path to location specified by Tor's <c>--ControlPortWriteToFile</c> invocation argument.</param>
		/// <exception cref="TorControlException">When getting or storing the Tor control port fails for whatever reason.</exception>
		private int ProcessControlPortFileCreatedByTor(string tempPath)
		{
			try
			{
				int? portNumber = null;

				// Parse file and close it so that it can be deleted later.
				// File is read line by line which is an approach that makes sense given Tor man page.
				// Yet, implementation is that:
				// 1. Tor creates a file with "<your-tor-control-port-file>.tmp" extension
				// 2. Writes to it
				// 3. Renames: "<your-tor-control-port-file>.tmp" -> "<your-tor-control-port-file>".
				// https://github.com/torproject/tor/blob/e247aab4eceeb3920f1667bf5a11d5bc83b950cc/src/feature/control/control.c#L159
				// https://github.com/torproject/tor/blob/e247aab4eceeb3920f1667bf5a11d5bc83b950cc/src/lib/fs/files.c#L337
				using (StreamReader file = new(tempPath))
				{
					string? line;

					while ((line = file.ReadLine()) != null)
					{
						if (line.StartsWith("PORT=", StringComparison.Ordinal))
						{
							// Use colon (":") to parse the port out of "PORT=127.0.0.1:58771" line.
							portNumber = int.Parse(line[(line.LastIndexOf(':') + 1)..]);
							break;
						}
					}
				}

				if (portNumber is null)
				{
					throw new TorControlException($"Could not found a line with a port in file '{tempPath}'");
				}

				// Store the port we got. Rewrites the file.
				File.WriteAllText(Settings.ControlPortFilePath, portNumber.ToString(), encoding: Encoding.UTF8);

				// Delete the temporary. This is not strictly necessary as Tor does that too but on Tor's exit.
				// https://github.com/torproject/tor/blob/e247aab4eceeb3920f1667bf5a11d5bc83b950cc/src/app/main/shutdown.c#L67
				File.Delete(tempPath);

				return portNumber.Value;
			}
			catch (TorControlException)
			{
				throw;
			}
			catch (Exception e)
			{
				throw new TorControlException($"Failed to read Tor control port from '{tempPath}'.", e);
			}
		}

		/// <summary>
		/// Connects to Tor control using a TCP client or throws <see cref="TorControlException"/>.
		/// </summary>
		/// <exception cref="TorControlException">When authentication fails for some reason.</exception>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">This method follows instructions in 3.23. TAKEOWNERSHIP.</seealso>
		private async Task<TorControlClient> InitTorControlAsync(int port, CancellationToken token = default)
		{
			// Get cookie.
			string cookieString = ByteHelpers.ToHex(File.ReadAllBytes(Settings.CookieAuthFilePath));

			// Authenticate.
			TorControlClientFactory factory = new();
			IPEndPoint controlEndpoint = new(IPAddress.Loopback, port);
			TorControlClient client = await factory.ConnectAndAuthenticateAsync(controlEndpoint, cookieString, token).ConfigureAwait(false);

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
