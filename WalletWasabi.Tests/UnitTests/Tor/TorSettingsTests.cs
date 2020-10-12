using System.Net;
using System.Runtime.InteropServices;
using WalletWasabi.Tor;
using Xunit;
using System.IO;

namespace WalletWasabi.Tests.UnitTests.Tor
{
	/// <summary>
	/// Tests for <see cref="Tor.TorSettings"/> class.
	/// </summary>
	public class TorSettingsTests
	{
		[Fact]
		public void GetCmdArgumentsTest()
		{
			var settings = new TorSettings(Path.Combine("temp", "tempDataDir"), Path.Combine("temp", "Tor.log"), "tempDistributionDir");
			var endpoint = new IPEndPoint(IPAddress.Loopback, WalletWasabi.Helpers.Constants.DefaultTorSocksPort);

			string arguments = settings.GetCmdArguments(endpoint);

			string expected = string.Join(
				" ",
				$"--SOCKSPort 127.0.0.1:9050",
				$"--DataDirectory \"{Path.Combine("temp", "tempDataDir", "tordata")}\"",
				$"--GeoIPFile \"{Path.Combine("tempDistributionDir", "Tor", "Geoip", "geoip")}\"",
				$"--GeoIPv6File \"{Path.Combine("tempDistributionDir", "Tor", "Geoip", "geoip6")}\"",
				$"--Log \"notice file {Path.Combine("temp", "Tor.log")}\"");

			Assert.Equal(expected, arguments);
		}
	}
}
