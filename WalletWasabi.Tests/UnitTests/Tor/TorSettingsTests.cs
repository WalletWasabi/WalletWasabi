using System.IO;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor
{
	/// <summary>
	/// Tests for <see cref="TorSettings"/> class.
	/// </summary>
	public class TorSettingsTests
	{
		[Fact]
		public void GetCmdArgumentsTest()
		{
			string dataDir = Path.Combine("temp", "tempDataDir");
			string logFilePath = Path.Combine("temp", "Tor.log");
			string distributionFolder = "tempDistributionDir";

			TorSettings settings = new(dataDir, logFilePath, distributionFolder, terminateOnExit: true, owningProcessId: 7);

			string arguments = settings.GetCmdArguments();

			string expected = string.Join(
				" ",
				$"--SOCKSPort 127.0.0.1:37150",
				$"--CookieAuthentication 1",
				$"--ControlPort auto",
				$"--ControlPortWriteToFile \"{settings.ControlPortRandomFilePath}\"",
				$"--CookieAuthFile \"{Path.Combine("temp", "tempDataDir", "control_auth_cookie")}\"",
				$"--DataDirectory \"{Path.Combine("temp", "tempDataDir", "tordata2")}\"",
				$"--GeoIPFile \"{Path.Combine("tempDistributionDir", "Tor", "Geoip", "geoip")}\"",
				$"--GeoIPv6File \"{Path.Combine("tempDistributionDir", "Tor", "Geoip", "geoip6")}\"",
				$"--Log \"notice file {Path.Combine("temp", "Tor.log")}\"",
				$"__OwningControllerProcess 7");

			Assert.Equal(expected, arguments);
		}
	}
}
