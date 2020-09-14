using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Exceptions;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Installs, starts and monitors Tor program.
	/// </summary>
	/// <seealso href="https://2019.www.torproject.org/docs/tor-manual.html.en"/>
	public class TorProcessManager
	{
		private const long StateNotStarted = 0;

		private const long StateRunning = 1;

		private const long StateStopping = 2;

		private const long StateStopped = 3;

		/// <summary>
		/// Value can be any of: <see cref="StateNotStarted"/>, <see cref="StateRunning"/>, <see cref="StateStopping"/> and <see cref="StateStopped"/>.
		/// </summary>
		private long _monitorState;

		/// <summary>
		/// Creates new instance of the object.
		/// </summary>
		/// <param name="settings">Tor settings.</param>
		/// <param name="torSocks5EndPoint">Valid Tor end point.</param>
		public TorProcessManager(TorSettings settings, EndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = torSocks5EndPoint;
			_monitorState = StateNotStarted;
			Stop = new CancellationTokenSource();
			TorProcess = null;
			Settings = settings;
		}

		/// <summary>
		/// If null then it's just a mock, clearnet is used.
		/// </summary>
		public EndPoint TorSocks5EndPoint { get; }

		public static bool RequestFallbackAddressUsage { get; private set; } = false;

		private Process? TorProcess { get; set; }

		private TorSettings Settings { get; }

		public bool IsRunning => Interlocked.Read(ref _monitorState) == StateRunning;

		private CancellationTokenSource Stop { get; set; }

		public static TorProcessManager Mock() // Mock, do not use Tor at all for debug.
		{
			return new TorProcessManager(null, null);
		}

		public void Start(bool ensureRunning)
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

						var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

						if (!File.Exists(Settings.TorPath))
						{
							Logger.LogInfo($"Tor instance NOT found at '{Settings.TorPath}'. Attempting to acquire it ...");
							TorInstallator.InstallAsync(Settings.TorDir).GetAwaiter().GetResult();
						}
						else if (!IoHelpers.CheckExpectedHash(Settings.HashSourcePath, Path.Combine(fullBaseDirectory, "TorDaemons")))
						{
							Logger.LogInfo($"Updating Tor...");

							string backupTorDir = $"{Settings.TorDir}_backup";
							if (Directory.Exists(backupTorDir))
							{
								Directory.Delete(backupTorDir, true);
							}
							Directory.Move(Settings.TorDir, backupTorDir);

							TorInstallator.InstallAsync(Settings.TorDir).GetAwaiter().GetResult();
						}
						else
						{
							Logger.LogInfo($"Tor instance found at '{Settings.TorPath}'.");
						}

						string torArguments = Settings.GetCmdArguments(TorSocks5EndPoint);

						if (Settings.LogFilePath is { })
						{
							IoHelpers.EnsureContainingDirectoryExists(Settings.LogFilePath);
							var logFileFullPath = Path.GetFullPath(Settings.LogFilePath);
							torArguments += $" --Log \"notice file {logFileFullPath}\"";
						}

						if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						{
							TorProcess = Process.Start(new ProcessStartInfo
							{
								FileName = Settings.TorPath,
								Arguments = torArguments,
								UseShellExecute = false,
								CreateNoWindow = true,
								RedirectStandardOutput = true
							});
							Logger.LogInfo($"Starting Tor process with Process.Start.");
						}
						else // Linux and OSX
						{
							string runTorCmd = $"LD_LIBRARY_PATH=$LD_LIBRARY_PATH:='{Settings.TorDir}/Tor' && export LD_LIBRARY_PATH && cd '{Settings.TorDir}/Tor' && ./tor {torArguments}";
							EnvironmentHelpers.ShellExecAsync(runTorCmd, waitForExit: false).GetAwaiter().GetResult();
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

		#region Monitor

		public void StartMonitor(TimeSpan torMisbehaviorCheckPeriod, TimeSpan checkIfRunningAfterTorMisbehavedFor, Uri fallBackTestRequestUri)
		{
			if (TorSocks5EndPoint is null)
			{
				return;
			}

			Logger.LogInfo("Starting Tor monitor...");
			if (Interlocked.CompareExchange(ref _monitorState, StateRunning, StateNotStarted) != StateNotStarted)
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

							if (TorHttpClient.TorDoesntWorkSince is { }) // If Tor misbehaves.
							{
								TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - TorHttpClient.TorDoesntWorkSince ?? TimeSpan.Zero;

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
										Start(ensureRunning: true); // Try starting Tor, if it does not work it'll be another issue.
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
					Interlocked.CompareExchange(ref _monitorState, StateStopped, StateStopping); // If IsStopping, make it stopped.
				}
			});
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _monitorState, StateStopping, StateRunning); // If running, make it stopping.

			if (TorSocks5EndPoint is null)
			{
				Interlocked.Exchange(ref _monitorState, StateStopped);
			}

			Stop?.Cancel();
			while (Interlocked.CompareExchange(ref _monitorState, StateStopped, StateNotStarted) == StateStopping)
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
