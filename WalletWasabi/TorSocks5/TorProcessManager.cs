using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.TorSocks5
{
	public class TorProcessManager
	{
		/// <summary>
		/// If null then it's just a mock, clearnet is used.
		/// </summary>
		public EndPoint TorSocks5EndPoint { get; }

		public string LogFile { get; }

		public static bool RequestFallbackAddressUsage { get; private set; } = false;

		public Process TorProcess { get; private set; }

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		/// <param name="logFile">Opt out of logging with null.</param>
		public TorProcessManager(EndPoint torSocks5EndPoint, string logFile)
		{
			TorSocks5EndPoint = torSocks5EndPoint;
			LogFile = logFile;
			_running = 0;
			Stop = new CancellationTokenSource();
			TorProcess = null;
		}

		public static TorProcessManager Mock() // Mock, do not use Tor at all for debug.
		{
			return new TorProcessManager(null, null);
		}

		public void Start(bool ensureRunning, string dataDir)
		{
			if (TorSocks5EndPoint is null)
			{
				return;
			}

			new Thread(delegate () // Do not ask. This is the only way it worked on Win10/Ubuntu18.04/Manjuro(1 processor VM)/Fedora(1 processor VM)
			{
				try
				{
					// 1. Is it already running?
					// 2. Can I simply run it from output directory?
					// 3. Can I copy and unzip it from assets?
					// 4. Throw exception.

					try
					{
						if (IsTorRunningAsync(TorSocks5EndPoint).GetAwaiter().GetResult())
						{
							Logger.LogInfo("Tor is already running.");
							return;
						}

						var torDir = Path.Combine(dataDir, "tor");
						var torPath = "";
						var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
						if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						{
							if (!fullBaseDirectory.StartsWith('/'))
							{
								fullBaseDirectory = fullBaseDirectory.Insert(0, "/");
							}

							torPath = $@"{torDir}/Tor/tor";
						}
						else // If Windows
						{
							torPath = $@"{torDir}\Tor\tor.exe";
						}

						if (!File.Exists(torPath))
						{
							Logger.LogInfo($"Tor instance NOT found at {torPath}. Attempting to acquire it...");
							InstallTor(fullBaseDirectory, torDir);
						}
						else if (!IoHelpers.CheckExpectedHash(torPath, Path.Combine(fullBaseDirectory, "TorDaemons")))
						{
							Logger.LogInfo($"Updating Tor...");

							string backupTorDir = $"{torDir}_backup";
							if (Directory.Exists(backupTorDir))
							{
								Directory.Delete(backupTorDir, true);
							}
							Directory.Move(torDir, backupTorDir);

							InstallTor(fullBaseDirectory, torDir);
						}
						else
						{
							Logger.LogInfo($"Tor instance found at {torPath}.");
						}

						string torArguments = $"--SOCKSPort {TorSocks5EndPoint}";
						if (!string.IsNullOrEmpty(LogFile))
						{
							IoHelpers.EnsureContainingDirectoryExists(LogFile);
							var logFileFullPath = Path.GetFullPath(LogFile);
							torArguments += $" --Log \"notice file {logFileFullPath}\"";
						}

						if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						{
							TorProcess = Process.Start(new ProcessStartInfo
							{
								FileName = torPath,
								Arguments = torArguments,
								UseShellExecute = false,
								CreateNoWindow = true,
								RedirectStandardOutput = true
							});
							Logger.LogInfo($"Starting Tor process with Process.Start.");
						}
						else // Linux and OSX
						{
							string runTorCmd = $"LD_LIBRARY_PATH=$LD_LIBRARY_PATH:={torDir}/Tor && export LD_LIBRARY_PATH && cd {torDir}/Tor && ./tor {torArguments}";
							EnvironmentHelpers.ShellExecAsync(runTorCmd, false).GetAwaiter().GetResult();
							Logger.LogInfo($"Started Tor process with shell command: {runTorCmd}.");
						}

						if (ensureRunning)
						{
							Task.Delay(3000).ConfigureAwait(false).GetAwaiter().GetResult(); // dotnet brainfart, ConfigureAwait(false) IS NEEDED HERE otherwise (only on) Manjuro Linux fails, WTF?!!
							if (!IsTorRunningAsync(TorSocks5EndPoint).GetAwaiter().GetResult())
							{
								throw new TorException("Attempted to start Tor, but it is not running.");
							}
							Logger.LogInfo("Tor is running.");
						}
					}
					catch (Exception ex)
					{
						throw new TorException("Could not automatically start Tor. Try running Tor manually.", ex);
					}
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}
			}).Start();
		}

		private static void InstallTor(string fullBaseDirectory, string torDir)
		{
			string torDaemonsDir = Path.Combine(fullBaseDirectory, "TorDaemons");

			string dataZip = Path.Combine(torDaemonsDir, "data-folder.zip");
			IoHelpers.BetterExtractZipToDirectoryAsync(dataZip, torDir).GetAwaiter().GetResult();
			Logger.LogInfo($"Extracted {dataZip} to {torDir}.");

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string torWinZip = Path.Combine(torDaemonsDir, "tor-win32.zip");
				IoHelpers.BetterExtractZipToDirectoryAsync(torWinZip, torDir).GetAwaiter().GetResult();
				Logger.LogInfo($"Extracted {torWinZip} to {torDir}.");
			}
			else // Linux or OSX
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					string torLinuxZip = Path.Combine(torDaemonsDir, "tor-linux64.zip");
					IoHelpers.BetterExtractZipToDirectoryAsync(torLinuxZip, torDir).GetAwaiter().GetResult();
					Logger.LogInfo($"Extracted {torLinuxZip} to {torDir}.");
				}
				else // OSX
				{
					string torOsxZip = Path.Combine(torDaemonsDir, "tor-osx64.zip");
					IoHelpers.BetterExtractZipToDirectoryAsync(torOsxZip, torDir).GetAwaiter().GetResult();
					Logger.LogInfo($"Extracted {torOsxZip} to {torDir}.");
				}

				// Make sure there's sufficient permission.
				string chmodTorDirCmd = $"chmod -R 750 {torDir}";
				EnvironmentHelpers.ShellExecAsync(chmodTorDirCmd).GetAwaiter().GetResult();
				Logger.LogInfo($"Shell command executed: {chmodTorDirCmd}.");
			}
		}

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		public static async Task<bool> IsTorRunningAsync(EndPoint torSocks5EndPoint)
		{
			using var client = new TorSocks5Client(torSocks5EndPoint);
			try
			{
				await client.ConnectAsync().ConfigureAwait(false);
				await client.HandshakeAsync().ConfigureAwait(false);
			}
			catch (ConnectionException)
			{
				return false;
			}
			return true;
		}

		public async Task<bool> IsTorRunningAsync()
		{
			if (TorSocks5EndPoint is null)
			{
				return true;
			}

			using var client = new TorSocks5Client(TorSocks5EndPoint);
			try
			{
				await client.ConnectAsync().ConfigureAwait(false);
				await client.HandshakeAsync().ConfigureAwait(false);
			}
			catch (ConnectionException)
			{
				return false;
			}
			return true;
		}

		#region Monitor

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		private CancellationTokenSource Stop { get; set; }

		public void StartMonitor(TimeSpan torMisbehaviorCheckPeriod, TimeSpan checkIfRunningAfterTorMisbehavedFor, string dataDirToStartWith, Uri fallBackTestRequestUri)
		{
			if (TorSocks5EndPoint is null)
			{
				return;
			}

			Logger.LogInfo("Starting Tor monitor...");
			if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
			{
				return;
			}

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							await Task.Delay(torMisbehaviorCheckPeriod, Stop.Token).ConfigureAwait(false);

							if (TorHttpClient.TorDoesntWorkSince != null) // If Tor misbehaves.
							{
								TimeSpan torMisbehavedFor = (DateTimeOffset.UtcNow - TorHttpClient.TorDoesntWorkSince) ?? TimeSpan.Zero;

								if (torMisbehavedFor > checkIfRunningAfterTorMisbehavedFor)
								{
									if (TorHttpClient.LatestTorException is TorSocks5FailureResponseException torEx)
									{
										if (torEx.RepField == RepField.HostUnreachable)
										{
											Uri baseUri = new Uri($"{fallBackTestRequestUri.Scheme}://{fallBackTestRequestUri.DnsSafeHost}");
											using (var client = new TorHttpClient(baseUri, TorSocks5EndPoint))
											{
												var message = new HttpRequestMessage(HttpMethod.Get, fallBackTestRequestUri);
												await client.SendAsync(message, Stop.Token).ConfigureAwait(false);
											}

											// Check if it changed in the meantime...
											if (TorHttpClient.LatestTorException is TorSocks5FailureResponseException torEx2 && torEx2.RepField == RepField.HostUnreachable)
											{
												// Fallback here...
												RequestFallbackAddressUsage = true;
											}
										}
									}
									else
									{
										Logger.LogInfo($"Tor did not work properly for {(int)torMisbehavedFor.TotalSeconds} seconds. Maybe it crashed. Attempting to start it...");
										Start(true, dataDirToStartWith); // Try starting Tor, if it does not work it'll be another issue.
										await Task.Delay(14000, Stop.Token).ConfigureAwait(false);
									}
								}
							}
						}
						catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
						{
							Logger.LogTrace(ex);
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.

			if (TorSocks5EndPoint is null)
			{
				Interlocked.Exchange(ref _running, 3);
			}

			Stop?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50).ConfigureAwait(false);
			}
			Stop?.Dispose();
			Stop = null;
			TorProcess?.Dispose();
			TorProcess = null;
		}

		#endregion Monitor
	}
}
