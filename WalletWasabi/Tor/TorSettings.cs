using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// All Tor-related settings.
	/// </summary>
	public class TorSettings
	{
		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="dataDir">Application data directory.</param>
		/// <param name="logFilePath">Full Tor log file path.</param>
		/// <param name="distributionFolderPath">Full path to folder containing Tor installation files.</param>
		public TorSettings(string dataDir, string logFilePath, string distributionFolderPath, bool terminateOnExit, int? owningProcessId = null)
		{
			TorBinaryFilePath = GetTorBinaryFilePath();
			TorBinaryDir = Path.Combine(MicroserviceHelpers.GetBinaryFolder(), "Tor");

			TorDataDir = Path.Combine(dataDir, "tordata2");
			CookieAuthFilePath = Path.Combine(dataDir, "control_auth_cookie");
			LogFilePath = logFilePath;
			IoHelpers.EnsureContainingDirectoryExists(LogFilePath);
			DistributionFolder = distributionFolderPath;
			TerminateOnExit = terminateOnExit;
			OwningProcessId = owningProcessId;
			GeoIpPath = Path.Combine(DistributionFolder, "Tor", "Geoip", "geoip");
			GeoIp6Path = Path.Combine(DistributionFolder, "Tor", "Geoip", "geoip6");

			ControlPortRandomFilePath = Path.Combine(dataDir, Path.GetRandomFileName());
			ControlPortFilePath = Path.Combine(dataDir, "TorControlPort.txt");
		}

		/// <summary>Full directory path where Tor binaries are placed.</summary>
		public string TorBinaryDir { get; }

		/// <summary>Full directory path where Tor stores its data.</summary>
		public string TorDataDir { get; }

		/// <summary>Full path. Directory may not necessarily exist.</summary>
		public string LogFilePath { get; }

		/// <summary>Full Tor distribution folder where Tor installation files are located.</summary>
		public string DistributionFolder { get; }

		/// <summary>Whether Tor should be terminated when Wasabi Wallet terminates.</summary>
		public bool TerminateOnExit { get; }

		/// <summary>Owning process ID for Tor program.</summary>
		public int? OwningProcessId { get; }

		/// <summary>Full path to executable file that is used to start Tor process.</summary>
		public string TorBinaryFilePath { get; }

		/// <summary>Full path to Tor cookie file.</summary>
		public string CookieAuthFilePath { get; }

		/// <summary>Tor control endpoint.</summary>
		public IPEndPoint SocksEndpoint { get; } = new(IPAddress.Loopback, 37150);

		/// <summary>Full path to a random file where Tor can store Tor control port value.</summary>
		/// <remarks>Note that it is recommended that this path should be in a location where other users should not have access to.</remarks>
		public string ControlPortRandomFilePath { get; }

		/// <summary>Full path to file containing port on which Tor control listens after Tor is started by Wasabi Wallet.</summary>
		/// <remarks>This file is supposed to contain last Tor contrl port we got from Tor.</remarks>
		public string ControlPortFilePath { get; }

		private string GeoIpPath { get; }
		private string GeoIp6Path { get; }

		/// <returns>Full path to Tor binary for selected <paramref name="platform"/>.</returns>
		public static string GetTorBinaryFilePath(OSPlatform? platform = null)
		{
			platform ??= MicroserviceHelpers.GetCurrentPlatform();

			string binaryPath = MicroserviceHelpers.GetBinaryPath(Path.Combine("Tor", "tor"), platform);
			return platform == OSPlatform.OSX ? $"{binaryPath}.real" : binaryPath;
		}

		/// <summary>Gets Tor control port that we stored when Tor was started.</summary>
		/// <exception cref="InvalidOperationException">When the port cannot be read for whatever reason.</exception>
		public int GetTorControlPort()
		{
			try
			{
				string content = File.ReadAllText(ControlPortFilePath, encoding: Encoding.UTF8);
				return int.Parse(content);
			}
			catch (Exception e)
			{
				Logger.LogError($"Failed to read Tor control port from file '{ControlPortFilePath}'", e);
				throw new InvalidOperationException($"Failed to read Tor control port from file '{ControlPortFilePath}'.", e);
			}
		}

		public string GetCmdArguments()
		{
			List<string> arguments = new()
			{
				$"--SOCKSPort {SocksEndpoint}",
				$"--CookieAuthentication 1",
				$"--ControlPort auto",
				$"--ControlPortWriteToFile \"{ControlPortRandomFilePath}\"",
				$"--CookieAuthFile \"{CookieAuthFilePath}\"",
				$"--DataDirectory \"{TorDataDir}\"",
				$"--GeoIPFile \"{GeoIpPath}\"",
				$"--GeoIPv6File \"{GeoIp6Path}\"",
				$"--Log \"notice file {LogFilePath}\""
			};

			if (TerminateOnExit && OwningProcessId is not null)
			{
				arguments.Add($"__OwningControllerProcess {OwningProcessId}");
			}

			return string.Join(" ", arguments);
		}
	}
}
