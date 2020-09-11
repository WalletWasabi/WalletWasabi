using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tor
{
	public class TorSettings
	{
		public TorSettings(string dataDir)
		{
			TorDir = Path.Combine(dataDir, "tor");
			TorDataDir = Path.Combine(dataDir, "tordata");

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				TorPath = $@"{TorDir}\Tor\tor.exe";
				HashSourcePath = $@"{TorDir}\Tor\tor.exe";
				GeoIpPath = $@"{TorDir}\Data\Tor\geoip";
				GeoIp6Path = $@"{TorDir}\Data\Tor\geoip6";
			}
			else
			{
				TorPath = $@"{TorDir}/Tor/tor";
				HashSourcePath = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
					? $@"{TorDir}/Tor/tor.real"
					: $@"{TorDir}/Tor/tor";
				GeoIpPath = $@"{TorDir}/Data/Tor/geoip";
				GeoIp6Path = $@"{TorDir}/Data/Tor/geoip6";
			}
		}

		public string TorDir { get; }
		public string TorDataDir { get; }
		public string HashSourcePath { get; }
		public string TorPath { get; }
		private string GeoIpPath { get; }
		private string GeoIp6Path { get; }

		public string GetCmdArguments(EndPoint torSocks5EndPoint)
		{
			return $"--SOCKSPort {torSocks5EndPoint} " +
				$"--DataDirectory \"{TorDataDir}\" " +
				$"--GeoIPFile \"{GeoIpPath}\" GeoIPv6File \"{GeoIp6Path}\"";
		}
	}
}
