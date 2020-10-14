using System.IO;
using System.Net;
using System.Runtime.InteropServices;
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
		public TorSettings(string dataDir, string logFilePath, string distributionFolderPath)
		{
			var torBinary = MicroserviceHelpers.GetBinaryPath(Path.Combine("Tor", "tor"));
			TorBinaryFilePath = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"{torBinary}.real" : torBinary;
			TorBinaryDir = Path.Combine(MicroserviceHelpers.GetBinaryFolder(), "Tor");

			TorDataDir = Path.Combine(dataDir, "tordata");
			LogFilePath = logFilePath;
			DistributionFolder = distributionFolderPath;

			GeoIpPath = Path.Combine(DistributionFolder, "Tor", "Geoip", "geoip");
			GeoIp6Path = Path.Combine(DistributionFolder, "Tor", "Geoip", "geoip6");
		}

		/// <summary>Full directory path where Tor binaries are placed.</summary>
		public string TorBinaryDir { get; }

		/// <summary>Full directory path where Tor stores its data.</summary>
		public string TorDataDir { get; }

		/// <summary>Full path. Directory may not necessarily exist.</summary>
		public string LogFilePath { get; }

		/// <summary>Full Tor distribution folder where Tor installation files are located.</summary>
		public string DistributionFolder { get; }

		/// <summary>Full path to executable file that is used to start Tor process.</summary>
		public string TorBinaryFilePath { get; }

		private string GeoIpPath { get; }
		private string GeoIp6Path { get; }

		public string GetCmdArguments(EndPoint torSocks5EndPoint)
		{
			return string.Join(
				" ",
				$"--SOCKSPort {torSocks5EndPoint}",
				$"--DataDirectory \"{TorDataDir}\"",
				$"--GeoIPFile \"{GeoIpPath}\"",
				$"--GeoIPv6File \"{GeoIp6Path}\"",
				$"--Log \"notice file {LogFilePath}\"");
		}
	}
}
