using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.TorSocks5
{
	public class TorProcessManager
	{
		public IPEndPoint TorSocks5EndPoint { get; }
		public string LogFile { get; }

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		/// <param name="logFile">Opt out of logging with null.</param>
		public TorProcessManager(IPEndPoint torSocks5EndPoint, string logFile)
		{
			TorSocks5EndPoint = torSocks5EndPoint ?? new IPEndPoint(IPAddress.Loopback, 9050);
			LogFile = logFile;
		}

		public async Task StartAsync()
		{
			// 1. Is it already running?
			// 2. Can I simply run it from output directory?
			// 3. Can I copy and unzip it from assets?
			// 4. Throw exception.

			try
			{
				if (await IsTorRunningAsync(TorSocks5EndPoint))
				{
					Logger.LogInfo<TorProcessManager>("Tor is already running.");
					return;
				}

				var torDir = Path.Combine(AppContext.BaseDirectory, "tor");
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
					var torDaemonsDir = $"TorDaemons";

					string dataZip = Path.Combine(torDaemonsDir, "data-folder.zip");
					await IoHelpers.BetterExtractZipToDirectoryAsync(dataZip, torDir);
					Logger.LogInfo<TorProcessManager>($"Extracted {dataZip} to {torDir}.");

					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						string torWinZip = Path.Combine(torDaemonsDir, "tor-win32.zip");
						await IoHelpers.BetterExtractZipToDirectoryAsync(torWinZip, torDir);
						Logger.LogInfo<TorProcessManager>($"Extracted {torWinZip} to {torDir}.");
					}
					else // Linux or OSX
					{
						if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
						{
							string torLinuxZip = "";
							if (RuntimeInformation.OSArchitecture == Architecture.X86)
							{
								torLinuxZip = Path.Combine(torDaemonsDir, "tor-linux32.zip");
							}
							else // x64
							{
								torLinuxZip = Path.Combine(torDaemonsDir, "tor-linux64.zip");
							}
							await IoHelpers.BetterExtractZipToDirectoryAsync(torLinuxZip, torDir);
							Logger.LogInfo<TorProcessManager>($"Extracted {torLinuxZip} to {torDir}.");
						}
						else // OSX
						{
							string torOsxZip = Path.Combine(torDaemonsDir, "tor-osx64.zip");
							await IoHelpers.BetterExtractZipToDirectoryAsync(torOsxZip, torDir);
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

				string torArguments = $"--SOCKSPort {TorSocks5EndPoint.Port}";
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
					Process.Start(torProcessStartInfo);
					Logger.LogInfo<TorProcessManager>($"Starting Tor process with Process.Start.");
				}
				else // Linux and OSX
				{
					string runTorCmd = $"LD_LIBRARY_PATH=$LD_LIBRARY_PATH:={torDir}/Tor && export LD_LIBRARY_PATH && cd {torDir}/Tor && ./tor {torArguments}";
					EnvironmentHelpers.ShellExec(runTorCmd, false);
					Logger.LogInfo<TorProcessManager>($"Started Tor process with shell command: {runTorCmd}.");
				}

				await Task.Delay(1000).ConfigureAwait(false); // dotnet brainfart, ConfigureAwait(false) IS NEEDED HERE otherwise (only on) Manjuro Linux fails, WTF?!!
				if (!await IsTorRunningAsync(TorSocks5EndPoint))
				{
					throw new TorException("Attempted to start Tor, but it is not running.");
				}
				Logger.LogInfo<TorProcessManager>("Tor is running.");
			}
			catch (Exception ex)
			{
				throw new TorException("Could not automatically start Tor. Try running Tor manually.", ex);
			}
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
	}
}
