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
		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="settings">Tor settings.</param>
		/// <param name="torSocks5EndPoint">Valid Tor end point.</param>
		public TorProcessManager(TorSettings settings, EndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = torSocks5EndPoint;
			TorProcess = null;
			Settings = settings;
			TorTcpConnectionFactory = new TorTcpConnectionFactory(torSocks5EndPoint);

			IoHelpers.EnsureContainingDirectoryExists(Settings.LogFilePath);
		}

		private EndPoint TorSocks5EndPoint { get; }

		private ProcessAsync? TorProcess { get; set; }

		private TorSettings Settings { get; }

		private TorTcpConnectionFactory TorTcpConnectionFactory { get; }

		private bool _disposed = false;

		public Task<bool> IsTorRunningAsync()
		{
			return TorTcpConnectionFactory.IsTorRunningAsync();
		}

		/// <summary>
		/// Starts Tor process if it is not running already.
		/// </summary>
		/// <param name="ensureRunning">
		/// If <c>false</c>, Tor is started but no attempt to verify that it actually runs is made.
		/// <para>If <c>true</c>, we start Tor and attempt to connect to it to verify it is running (at most 25 attempts).</para>
		/// </param>
		public async Task<bool> StartAsync(bool ensureRunning)
		{
			ThrowIfDisposed();

			try
			{
				// Is Tor already running? Either our Tor process from previous Wasabi Wallet run or possibly user's own Tor.
				bool isAlreadyRunning = await IsTorRunningAsync().ConfigureAwait(false);

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

				TorProcess = new ProcessAsync(startInfo);

				Logger.LogInfo($"Starting Tor process ...");
				TorProcess.Start();

				if (ensureRunning)
				{
					int i = 0;
					while (true)
					{
						i++;

						bool isRunning = await IsTorRunningAsync().ConfigureAwait(false);

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
						await Task.Delay(250).ConfigureAwait(false);
					}

					Logger.LogInfo("Tor is running.");
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Could not automatically start Tor. Try running Tor manually.", ex);
			}

			return false;
		}

		public async Task StopAsync(bool killTor = false)
		{
			if (TorProcess is { } && killTor)
			{
				Logger.LogInfo($"Killing Tor process.");

				try
				{
					TorProcess.Kill();
					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
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
