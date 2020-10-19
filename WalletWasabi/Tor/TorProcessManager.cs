using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Tor.Exceptions;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Starts and monitors Tor program.
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
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="settings">Tor settings.</param>
		/// <param name="torSocks5EndPoint">Valid Tor end point.</param>
		public TorProcessManager(TorSettings settings, EndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = torSocks5EndPoint;
			_monitorState = StateNotStarted;
			MonitorCts = new CancellationTokenSource();
			TorProcess = null;
			Settings = settings;
			TorSocks5Client = new TorSocks5Client(torSocks5EndPoint);

			IoHelpers.EnsureContainingDirectoryExists(Settings.LogFilePath);
		}

		private EndPoint TorSocks5EndPoint { get; }

		public static bool RequestFallbackAddressUsage { get; private set; } = false;

		private ProcessAsync? TorProcess { get; set; }

		private TorSettings Settings { get; }

		private TorSocks5Client TorSocks5Client { get; }

		public bool IsRunning => Interlocked.Read(ref _monitorState) == StateRunning;

		private CancellationTokenSource MonitorCts { get; set; }

		/// <summary>
		/// Installs Tor if it is not installed, then it starts Tor.
		/// </summary>
		/// <param name="ensureRunning">
		/// If <c>false</c>, Tor is started but no attempt to verify that it actually runs is made.
		/// <para>If <c>true</c>, we start Tor and attempt to connect to it to verify it is running (at most 25 attempts).</para>
		/// </param>
		public async Task<bool> StartAsync(bool ensureRunning)
		{
			try
			{
				// Is Tor already running? Either our Tor process from previous Wasabi Wallet run or possibly user's own Tor.
				bool isAlreadyRunning = await TorSocks5Client.IsTorRunningAsync().ConfigureAwait(false);

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

						bool isRunning = await TorSocks5Client.IsTorRunningAsync().ConfigureAwait(false);

						if (isRunning)
						{
							break;
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

		#region Monitor

		public void StartMonitor(TimeSpan torMisbehaviorCheckPeriod, TimeSpan checkIfRunningAfterTorMisbehavedFor, Uri fallBackTestRequestUri)
		{
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
							await Task.Delay(torMisbehaviorCheckPeriod, MonitorCts.Token).ConfigureAwait(false);

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
												await client.SendAsync(message, MonitorCts.Token).ConfigureAwait(false);
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
										await StartAsync(ensureRunning: true).ConfigureAwait(false); // Try starting Tor, if it does not work it'll be another issue.
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

		/// <summary>
		/// Stops Tor monitor, TCP connection with Tor and Tor process (if it was started).
		/// </summary>
		/// <param name="killTor">Whether to kill Tor process or whether it should continue running for privacy reasons.</param>
		public async Task StopAsync(bool killTor = false)
		{
			Logger.LogTrace($"> {nameof(killTor)}={killTor}");

			Interlocked.CompareExchange(ref _monitorState, StateStopping, StateRunning); // If running, make it stopping.

			MonitorCts.Cancel();
			while (Interlocked.CompareExchange(ref _monitorState, StateStopped, StateNotStarted) == StateStopping)
			{
				await Task.Delay(50).ConfigureAwait(false);
			}

			// Stop Tor monitor.
			MonitorCts.Dispose();

			// Stop TCP connection with Tor.
			TorSocks5Client.Dispose();

			// Stop Tor itself, if the option is selected by the user.
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

			Logger.LogTrace("<");
		}

		#endregion Monitor
	}
}
