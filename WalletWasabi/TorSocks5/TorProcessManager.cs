﻿using System;
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
	public class TorProcessManager : IDisposable
	{
		public IPEndPoint TorSocks5EndPoint { get; }
		public string LogFile { get; }

		public static bool RequestFallbackAddressUsage { get; private set; } = false;

		public Process TorProcess { get; private set; }

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		/// <param name="logFile">Opt out of logging with null.</param>
		public TorProcessManager(IPEndPoint torSocks5EndPoint, string logFile)
		{
			TorSocks5EndPoint = torSocks5EndPoint ?? new IPEndPoint(IPAddress.Loopback, 9050);
			LogFile = logFile;
			_running = 0;
			Stop = new CancellationTokenSource();
			TorProcess = null;
		}

		public void Start(bool ensureRunning, string dataDir)
		{
			new Thread(delegate () // Don't ask. This is the only way it worked on Win10/Ubuntu18.04/Manjuro(1 processor VM)/Fedora(1 processor VM)
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
							Logger.LogInfo<TorProcessManager>("Tor is already running.");
							return;
						}

						var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
						if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						{
							if (!fullBaseDirectory.StartsWith('/'))
							{
								fullBaseDirectory.Insert(0, "/");
							}
						}

						var torDir = Path.Combine(dataDir, "tor");

						var torPath = "";
						if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						{
							torPath = $@"{torDir}\Tor\tor.exe";
						}
						else // Linux or OSX
						{
							torPath = $@"{torDir}/Tor/tor";
						}

						if (!File.Exists(torPath))
						{
							Logger.LogInfo<TorProcessManager>($"Tor instance NOT found at {torPath}. Attempting to acquire it...");
							string torDaemonsDir = Path.Combine(fullBaseDirectory, "TorDaemons");

							string dataZip = Path.Combine(torDaemonsDir, "data-folder.zip");
							IoHelpers.BetterExtractZipToDirectoryAsync(dataZip, torDir).GetAwaiter().GetResult();
							Logger.LogInfo<TorProcessManager>($"Extracted {dataZip} to {torDir}.");

							if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
							{
								string torWinZip = Path.Combine(torDaemonsDir, "tor-win32.zip");
								IoHelpers.BetterExtractZipToDirectoryAsync(torWinZip, torDir).GetAwaiter().GetResult();
								Logger.LogInfo<TorProcessManager>($"Extracted {torWinZip} to {torDir}.");
							}
							else // Linux or OSX
							{
								if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
								{
									string torLinuxZip = torLinuxZip = Path.Combine(torDaemonsDir, "tor-linux64.zip");
									IoHelpers.BetterExtractZipToDirectoryAsync(torLinuxZip, torDir).GetAwaiter().GetResult();
									Logger.LogInfo<TorProcessManager>($"Extracted {torLinuxZip} to {torDir}.");
								}
								else // OSX
								{
									string torOsxZip = Path.Combine(torDaemonsDir, "tor-osx64.zip");
									IoHelpers.BetterExtractZipToDirectoryAsync(torOsxZip, torDir).GetAwaiter().GetResult();
									Logger.LogInfo<TorProcessManager>($"Extracted {torOsxZip} to {torDir}.");
								}

								// Make sure there's sufficient permission.
								string chmodTorDirCmd = $"chmod -R 777 {torDir}";
								EnvironmentHelpers.ShellExec(chmodTorDirCmd);
								Logger.LogInfo<TorProcessManager>($"Shell command executed: {chmodTorDirCmd}.");
							}
						}
						else
						{
							Logger.LogInfo<TorProcessManager>($"Tor instance found at {torPath}.");
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
							var torProcessStartInfo = new ProcessStartInfo(torPath)
							{
								Arguments = torArguments,
								UseShellExecute = false,
								CreateNoWindow = true,
								RedirectStandardOutput = true
							};
							TorProcess = Process.Start(torProcessStartInfo);
							Logger.LogInfo<TorProcessManager>($"Starting Tor process with Process.Start.");
						}
						else // Linux and OSX
						{
							string runTorCmd = $"LD_LIBRARY_PATH=$LD_LIBRARY_PATH:={torDir}/Tor && export LD_LIBRARY_PATH && cd {torDir}/Tor && ./tor {torArguments}";
							EnvironmentHelpers.ShellExec(runTorCmd, false);
							Logger.LogInfo<TorProcessManager>($"Started Tor process with shell command: {runTorCmd}.");
						}

						if (ensureRunning)
						{
							Task.Delay(3000).ConfigureAwait(false).GetAwaiter().GetResult(); // dotnet brainfart, ConfigureAwait(false) IS NEEDED HERE otherwise (only on) Manjuro Linux fails, WTF?!!
							if (!IsTorRunningAsync(TorSocks5EndPoint).GetAwaiter().GetResult())
							{
								throw new TorException("Attempted to start Tor, but it is not running.");
							}
							Logger.LogInfo<TorProcessManager>("Tor is running.");
						}
					}
					catch (Exception ex)
					{
						throw new TorException("Could not automatically start Tor. Try running Tor manually.", ex);
					}
				}
				catch (Exception ex)
				{
					Logger.LogError<TorProcessManager>(ex);
				}
			}).Start();
		}

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		public static async Task<bool> IsTorRunningAsync(IPEndPoint torSocks5EndPoint)
		{
			using (var client = new TorSocks5Client(torSocks5EndPoint))
			{
				try
				{
					await client.ConnectAsync();
					await client.HandshakeAsync();
				}
				catch (ConnectionException)
				{
					return false;
				}
				return true;
			}
		}

		public async Task<bool> IsTorRunningAsync()
		{
			using (var client = new TorSocks5Client(TorSocks5EndPoint))
			{
				try
				{
					await client.ConnectAsync();
					await client.HandshakeAsync();
				}
				catch (ConnectionException)
				{
					return false;
				}
				return true;
			}
		}

		#region Monitor

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Stop { get; }

		public void StartMonitor(TimeSpan torMisbehaviorCheckPeriod, TimeSpan checkIfRunningAfterTorMisbehavedFor, string dataDirToStartWith, Uri fallBackTestRequestUri)
		{
			Logger.LogInfo<TorProcessManager>("Starting Tor monitor...");
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							// If stop was requested return.
							if (IsRunning == false) return;

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
											using (var client = new TorHttpClient(new Uri(fallBackTestRequestUri.DnsSafeHost), TorSocks5EndPoint))
											{
												var message = new HttpRequestMessage(HttpMethod.Get, fallBackTestRequestUri);
												await client.SendAsync(message, Stop.Token);
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
										Logger.LogInfo<TorProcessManager>($"Tor didn't work properly for {(int)torMisbehavedFor.TotalSeconds} seconds. Maybe it crashed. Attempting to start it...");
										Start(true, dataDirToStartWith); // Try starting Tor, if doesn't work it'll be another issue.
										await Task.Delay(14000, Stop.Token).ConfigureAwait(false);
									}
								}
							}
						}
						catch (TaskCanceledException ex)
						{
							Logger.LogTrace<TorProcessManager>(ex);
						}
						catch (OperationCanceledException ex)
						{
							Logger.LogTrace<TorProcessManager>(ex);
						}
						catch (TimeoutException ex)
						{
							Logger.LogTrace<TorProcessManager>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogDebug<TorProcessManager>(ex);
						}
					}
				}
				finally
				{
					if (IsStopping)
					{
						Interlocked.Exchange(ref _running, 3);
					}
				}
			});
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (IsRunning)
					{
						Interlocked.Exchange(ref _running, 2);
					}
					Stop?.Cancel();
					while (IsStopping)
					{
						Task.Delay(50).GetAwaiter().GetResult(); // DO NOT MAKE IT ASYNC (.NET Core threading brainfart)
					}
					Stop?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support

		#endregion Monitor
	}
}
