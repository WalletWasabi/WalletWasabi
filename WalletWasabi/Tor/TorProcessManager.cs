using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Manages lifetime of Tor process.
	/// </summary>
	/// <seealso href="https://2019.www.torproject.org/docs/tor-manual.html.en"/>
	public class TorProcessManager
	{
		public TorProcessManager(TorSettings settings, EndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = torSocks5EndPoint;
			TorProcess = null;
			Settings = settings;
			TcpConnectionFactory = new(torSocks5EndPoint);
		}

		private EndPoint TorSocks5EndPoint { get; }

		private ProcessAsync? TorProcess { get; set; }

		private TorSettings Settings { get; }
		private TorTcpConnectionFactory TcpConnectionFactory { get; }

		private bool _disposed = false;

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
					return true;
				}

				string torArguments = Settings.GetCmdArguments(TorSocks5EndPoint);

				var startInfo = new ProcessStartInfo
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

		public async Task StopAsync()
		{
			if (TorProcess is { } && Settings.TerminateOnExit)
			{
				Logger.LogInfo($"Killing Tor process.");

				try
				{
					TorProcess.Kill();
					using CancellationTokenSource cts = new(TimeSpan.FromMinutes(1));
					await TorProcess.WaitForExitAsync(cts.Token, killOnCancel: true).ConfigureAwait(false);

					Logger.LogInfo($"Tor process killed successfully.");
				}
				catch (Exception ex)
				{
					Logger.LogError($"Could not kill Tor process: {ex.Message}.");
				}
			}

			// Dispose Tor process resources (does not stop/kill Tor process).
			TorProcess?.Dispose();

			_disposed = true;
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
