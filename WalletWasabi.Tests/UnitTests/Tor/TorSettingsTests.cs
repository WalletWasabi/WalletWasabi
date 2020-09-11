using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using WalletWasabi.Tor;
using Xunit;

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
			var settings = new TorSettings("temp");
			var endpoint = new IPEndPoint(IPAddress.Loopback, WalletWasabi.Helpers.Constants.DefaultTorSocksPort);

			string arguments = settings.GetCmdArguments(endpoint);
			string expected;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				expected = @"--SOCKSPort 127.0.0.1:9050 --DataDirectory ""temp\tordata"" --GeoIPFile ""temp\tor\Data\Tor\geoip"" GeoIPv6File ""temp\tor\Data\Tor\geoip6""";
			}
			else
			{
				expected = @"--SOCKSPort 127.0.0.1:9050 --DataDirectory ""temp/tordata"" --GeoIPFile ""temp/tor/Data/Tor/geoip"" GeoIPv6File ""temp/tor/Data/Tor/geoip6""";
			}

			Assert.Equal(expected, arguments);
		}
	}
}
