using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Microservices;

namespace WalletWasabi.Tor;

/// <summary>
/// All Tor-related settings.
/// </summary>
public class TorSettings
{
	/// <summary>Tor binary file name without extension.</summary>
	public const string TorBinaryFileName = "tor";

	/// <summary>Default port assigned to Tor SOCKS5 for the Wasabi's bundled Tor.</summary>
	public const int DefaultSocksPort = 37150;

	/// <summary>Default port assigned to Tor control for the Wasabi's bundled Tor.</summary>
	public const int DefaultControlPort = 37151;

	/// <param name="dataDir">Application data directory.</param>
	/// <param name="distributionFolderPath">Full path to folder containing Tor installation files.</param>
	public TorSettings(string dataDir, string distributionFolderPath, bool terminateOnExit, int socksPort = DefaultSocksPort, int controlPort = DefaultControlPort, int? owningProcessId = null)
	{
		TorBinaryFilePath = GetTorBinaryFilePath();
		TorBinaryDir = Path.Combine(MicroserviceHelpers.GetBinaryFolder(), "Tor");

		TorDataDir = Path.Combine(dataDir, "tordata2");
		SocksEndpoint = new IPEndPoint(IPAddress.Loopback, socksPort);
		ControlEndpoint = new IPEndPoint(IPAddress.Loopback, controlPort);

		bool defaultWasabiTorPorts = socksPort == DefaultSocksPort && controlPort == DefaultControlPort;
		CookieAuthFilePath = defaultWasabiTorPorts
			? Path.Combine(dataDir, $"control_auth_cookie")
			: Path.Combine(dataDir, $"control_auth_cookie_{socksPort}_{controlPort}");

		LogFilePath = Path.Combine(dataDir, "TorLogs.txt");
		IoHelpers.EnsureContainingDirectoryExists(LogFilePath);
		DistributionFolder = distributionFolderPath;
		TerminateOnExit = terminateOnExit;
		OwningProcessId = owningProcessId;
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

	/// <summary>Whether Tor should be terminated when Wasabi Wallet terminates.</summary>
	public bool TerminateOnExit { get; }

	/// <summary>Owning process ID for Tor program.</summary>
	public int? OwningProcessId { get; }

	/// <summary>Full path to executable file that is used to start Tor process.</summary>
	public string TorBinaryFilePath { get; }

	/// <summary>Full path to Tor cookie file.</summary>
	public string CookieAuthFilePath { get; }

	/// <summary>Tor SOCKS5 endpoint.</summary>
	public EndPoint SocksEndpoint { get; }

	/// <summary>Tor control endpoint.</summary>
	public EndPoint ControlEndpoint { get; }
	public int RpcVirtualPort => 80;
	public int RpcOnionPort => 37129;

	private string GeoIpPath { get; }
	private string GeoIp6Path { get; }

	/// <returns>Full path to Tor binary for selected <paramref name="platform"/>.</returns>
	public static string GetTorBinaryFilePath(OSPlatform? platform = null)
	{
		platform ??= MicroserviceHelpers.GetCurrentPlatform();

		return MicroserviceHelpers.GetBinaryPath(Path.Combine("Tor", TorBinaryFileName), platform);
	}

	/// <seealso href="https://github.com/torproject/tor/blob/7528524aee3ffe3c9b7c69fa18f659e1993f59a3/doc/man/tor.1.txt#L1505-L1509">For <c>KeepAliveIsolateSOCKSAuth</c> explanation.</seealso>
	/// <seealso href="https://github.com/torproject/tor/blob/22cb4c23d0d23dfda2c91817bac74a01831f94af/doc/man/tor.1.txt#L1298-L1305">
	/// Explains <c>MaxCircuitDirtiness</c> parameter which is affected by the <c>KeepAliveIsolateSOCKSAuth</c> flag.
	/// </seealso>
	public string GetCmdArguments()
	{
		if (!ControlEndpoint.TryGetPort(out int? port))
		{
			port = 9051; // Standard port for Tor control.
		}

		// `--SafeLogging 0` is useful for debugging to avoid "[scrubbed]" redactions in Tor log.
		List<string> arguments = new()
		{
			$"--LogTimeGranularity 1",
			$"--TruncateLogFile 1",
			$"--SOCKSPort \"{SocksEndpoint} ExtendedErrors KeepAliveIsolateSOCKSAuth\"",
			$"--MaxCircuitDirtiness 1800", // 30 minutes. Default is 10 minutes.
			$"--SocksTimeout 30", // Default is 2 minutes.
			$"--CookieAuthentication 1",
			$"--ControlPort {port}",
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
