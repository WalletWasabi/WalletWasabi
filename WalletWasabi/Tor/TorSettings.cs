using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

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

	public TorSettings(
		string dataDir,
		string distributionFolderPath,
		bool terminateOnExit,
		TorMode torMode = TorMode.Enabled,
		int socksPort = DefaultSocksPort,
		int controlPort = DefaultControlPort,
		string? torFolder = null,
		string[]? bridges = null,
		int? owningProcessId = null,
		bool log = true)
	{
		IsCustomTorFolder = torFolder is not null;

		bool defaultWasabiTorPorts = socksPort == DefaultSocksPort && controlPort == DefaultControlPort;

		if (defaultWasabiTorPorts)
		{
			// Use different ports when user overrides Tor folder to avoid accessing the same control_auth_cookie file.
			if (IsCustomTorFolder)
			{
				socksPort = 37152;
				controlPort = 37153;
			}
			else if (torMode == TorMode.EnabledOnlyRunning)
			{
				// Whonix & Tails use standard ports.
				socksPort = 9050;
				controlPort = 9051;
			}
		}

		TorBinaryDir = torFolder ?? Path.Combine(MicroserviceHelpers.GetBinaryFolder(), "Tor");
		TorBinaryFilePath = GetTorBinaryFilePath(TorBinaryDir);
		TorTransportPluginsDir = Path.Combine(TorBinaryDir, "PluggableTransports");

		TorDataDir = Path.Combine(dataDir, "tordata2");
		SocksEndpoint = new IPEndPoint(IPAddress.Loopback, socksPort);
		ControlEndpoint = new IPEndPoint(IPAddress.Loopback, controlPort);
		Bridges = bridges ?? [];
		OwningProcessId = owningProcessId;

		CookieAuthFilePath = defaultWasabiTorPorts
			? Path.Combine(dataDir, $"control_auth_cookie")
			: Path.Combine(dataDir, $"control_auth_cookie_{socksPort}_{controlPort}");

		LogFilePath = Path.Combine(dataDir, "TorLogs.txt");
		IoHelpers.EnsureContainingDirectoryExists(LogFilePath);
		DistributionFolder = distributionFolderPath;

		if (torMode == TorMode.EnabledOnlyRunning && terminateOnExit)
		{
			Logger.LogWarning("Wasabi is instructed to use a running Tor process. Terminate on exit was disabled.");
		}

		TorMode = torMode;
		TerminateOnExit = TorMode == TorMode.EnabledOnlyRunning ? false : terminateOnExit;

		Log = log;
		_geoIpPath = Path.Combine(DistributionFolder, "Tor", "Geoip", "geoip");
		_geoIp6Path = Path.Combine(DistributionFolder, "Tor", "Geoip", "geoip6");
	}

	public TorMode TorMode { get; }

	/// <summary><c>true</c> if user specified a custom Tor folder to run a (possibly) different Tor binary than the bundled Tor, <c>false</c> otherwise.</summary>
	public bool IsCustomTorFolder { get; }

	/// <summary>Full directory path where Tor binaries are placed.</summary>
	public string TorBinaryDir { get; }

	/// <summary>Full directory path where Tor transports plugins are placed.</summary>
	public string TorTransportPluginsDir { get; }

	/// <summary>Full directory path where Tor stores its data.</summary>
	public string TorDataDir { get; }

	/// <summary>Full path. Directory may not necessarily exist.</summary>
	public string LogFilePath { get; }

	/// <summary>Full Tor distribution folder where Tor installation files are located.</summary>
	public string DistributionFolder { get; }

	/// <summary>Whether Tor should be terminated when Wasabi Wallet terminates.</summary>
	public bool TerminateOnExit { get; }

	/// <summary>Array of bridges to use.</summary>
	/// <remarks>
	/// Syntax of each bridge definition is as in Tor's man - i.e. <c>**Bridge** [__transport__] __IP__:__ORPort__ [__fingerprint__]::</c>.
	/// <para>If the array is non-empty, Tor is asked to use those bridges. Otherwise, we do not turn on Tor's bridge support.</para>
	/// </remarks>
	public string[] Bridges { get; }

	/// <summary>Owning process ID for Tor program.</summary>
	public int? OwningProcessId { get; }

	/// <summary><c>true</c> if logging to TorLogs.txt file is enabled, <c>false</c> otherwise.</summary>
	public bool Log { get; }

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

	private readonly string _geoIpPath;
	private readonly string _geoIp6Path;

	/// <returns>Full path to Tor binary for selected <paramref name="platform"/>.</returns>
	public static string GetTorBinaryFilePath(string path, OSPlatform? platform = null)
	{
		return Path.Combine(path, MicroserviceHelpers.GetFilenameWithExtension(TorBinaryFileName, platform));
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

		bool useBridges = Bridges.Length > 0;

		// `--SafeLogging 0` is useful for debugging to avoid "[scrubbed]" redactions in Tor log.
		List<string> arguments = [
			$"--LogTimeGranularity 1",
			$"--TruncateLogFile 1",
			$"--UseBridges {(useBridges ? "1" : "0")}",
			$"--SOCKSPort \"{SocksEndpoint} ExtendedErrors KeepAliveIsolateSOCKSAuth\"",
			$"--MaxCircuitDirtiness 1800", // 30 minutes. Default is 10 minutes.
			$"--SocksTimeout 30", // Default is 2 minutes.
			$"--CookieAuthentication 1",
			$"--ControlPort {port}",
			$"--CookieAuthFile \"{CookieAuthFilePath}\"",
			$"--DataDirectory \"{TorDataDir}\"",
			$"--GeoIPFile \"{_geoIpPath}\"",
			$"--GeoIPv6File \"{_geoIp6Path}\"",
			$"--NumEntryGuards 3",
			$"--NumPrimaryGuards 3"
		];

		if (useBridges)
		{
			HashSet<string> usedPlugins = [];

			foreach (string bridge in Bridges)
			{
				if (bridge.Contains('\'') || bridge.Contains('"'))
				{
					Logger.LogError($"Skipping bridge '{bridge}' because it contains a quote or an apostrophe.");
					continue;
				}

				if (bridge.StartsWith("obfs4 ", StringComparison.Ordinal))
				{
					usedPlugins.Add("obfs4");
				}
				else if (bridge.StartsWith("webtunnel ", StringComparison.Ordinal))
				{
					usedPlugins.Add("webtunnel");
				}
				else if (bridge.StartsWith("snowflake ", StringComparison.Ordinal))
				{
					usedPlugins.Add("snowflake");
				}
				else
				{
					throw new NotSupportedException($"Bridge transport of bridge '{bridge}' is not supported.");
				}

				arguments.Add($"--Bridge \"{bridge}\"");
			}

			foreach (string plugin in usedPlugins)
			{
				string fileNameWithoutExtension = plugin switch
				{
					"obfs4" => "lyrebird", // obfs4 was renamed to lyrebird.
					"webtunnel" => "webtunnel-client",
					"snowflake" => "snowflake-client",
					_ => throw new NotSupportedException($"Unknown Tor pluggable transport '{plugin}'."),
				};

				string filename = MicroserviceHelpers.GetCurrentPlatform() == OSPlatform.Windows ? $"{fileNameWithoutExtension}.exe" : $"{fileNameWithoutExtension}";
				string path = Path.Combine(TorTransportPluginsDir, filename);

				if (!File.Exists(path))
				{
					throw new NotSupportedException($"Tor bridge plugin was not found '{path}'.");
				}

				arguments.Add($"--ClientTransportPlugin \"{plugin} exec {path}\"");
			}
		}

		if (Log)
		{
			arguments.Add($"--Log \"notice file {LogFilePath}\"");
		}

		if (TerminateOnExit && OwningProcessId is not null)
		{
			arguments.Add($"__OwningControllerProcess {OwningProcessId}");
		}

		return string.Join(" ", arguments);
	}
}
