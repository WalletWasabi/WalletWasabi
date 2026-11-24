using System.IO;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

/// <summary>
/// Tests for <see cref="TorSettings"/> class.
/// </summary>
public class TorSettingsTests
{
	[Fact]
	public void GetCmdArgumentsTest()
	{
		var dataDir = Path.Combine("temp", "tempDataDir");
		var distributionFolder = "tempDistributionDir";

		var settings = new TorSettings(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

		var arguments = settings.GetCmdArguments();

		var expected = string.Join(
			" ",
			$"--defaults-torrc \"{Path.Combine(dataDir, "torrc-defaults")}\"",
			$"-f \"{Path.Combine(dataDir, "torrc")}\"",
			$"--ignore-missing-torrc",
			$"--LogTimeGranularity 1",
			$"--TruncateLogFile 1",
			$"--UseBridges 0",
			$"--SOCKSPort \"127.0.0.1:37150 ExtendedErrors KeepAliveIsolateSOCKSAuth\"",
			$"--SocksTimeout 30",
			$"--CookieAuthentication 1",
			$"--ControlPort 37151",
			$"--CookieAuthFile \"{Path.Combine("temp", "tempDataDir", "control_auth_cookie")}\"",
			$"--DataDirectory \"{Path.Combine("temp", "tempDataDir", "tordata2")}\"",
			$"--GeoIPFile \"{Path.Combine("tempDistributionDir", "Tor", "Geoip", "geoip")}\"",
			$"--GeoIPv6File \"{Path.Combine("tempDistributionDir", "Tor", "Geoip", "geoip6")}\"",
			$"--NumEntryGuards 3",
			$"--NumPrimaryGuards 3",
			$"--ConfluxEnabled 1",
			$"--ConfluxClientUX throughput",
			$"--Log \"notice file {Path.Combine("temp", "tempDataDir", "TorLogs.txt")}\"",
			$"__OwningControllerProcess 7");

		Assert.Equal(expected, arguments);
	}
}
