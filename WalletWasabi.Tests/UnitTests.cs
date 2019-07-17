using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui;
using Xunit;

namespace WalletWasabi.Tests
{
	public class UnitTests
	{
		[Fact]
		public void CanParseHostEndpoint()
		{
			var input = new string[]
			{
				"tcp://5wyqrzbvrdsumnok.onion:3928",
				"bitcoin-p2p://5wyqrzbvrdsumnok.onion:3928",
				"bitcoin-p2p://5wyqrzbvrdsumnok.onion:3928/",
				"5wyqrzbvrdsumnok.onion:3928",
				"    5wyqrzbvrdsumnok.onion:3928    ",
				"bitcoin-p2p://nico:pass@5wyqrzbvrdsumnok.onion:3928",
				"bitcoin-p2p://5wyqrzbvrdsumnok.onion:3928/blahblah",
			};

			foreach (var i in input)
			{
				Assert.True(Config.TryParseEndpoint(i, 9999, out var e));
				var dns = Assert.IsType<DnsEndPoint>(e);
				Assert.Equal("5wyqrzbvrdsumnok.onion", dns.Host);
				Assert.Equal(3928, dns.Port);
			}
		}

		[Fact]
		public async Task CanMigrateTorConfigAsync()
		{
#pragma warning disable CS0612 // Type or member is obsolete
			var config = "{" +
				  "\"MainNetBitcoinCoreHost\": \"nico\"," +
				  "\"MainNetBitcoinCorePort\": 3928," +
				  "\"TestNetBitcoinCoreHost\": \"nico1\"," +
				  "\"TestNetBitcoinCorePort\": 3929," +
				  "\"RegTestBitcoinCoreHost\": \"nico2:22\"," +
				  "\"RegTestBitcoinCorePort\": 3930" +
				"}";

			var configFile = nameof(CanMigrateTorConfigAsync);
			File.WriteAllText(configFile, config);
			var configObject = new Config(configFile);
			await configObject.LoadOrCreateDefaultFileAsync();
			Assert.Equal("nico:3928", configObject.MainNetBitcoinCoreHost);
			Assert.Null(configObject.MainNetBitcoinCorePort);
			Assert.Equal("nico1:3929", configObject.TestNetBitcoinCoreHost);
			Assert.Null(configObject.TestNetBitcoinCorePort);
			Assert.Equal("nico2:22", configObject.RegTestBitcoinCoreHost);
			Assert.Null(configObject.RegTestBitcoinCorePort);
			await configObject.ToFileAsync();

			configObject = new Config(configFile);
			await configObject.LoadOrCreateDefaultFileAsync();
			Assert.DoesNotContain("TestNetBitcoinCorePort", File.ReadAllText(configFile));
			Assert.Equal("nico:3928", configObject.MainNetBitcoinCoreHost);
			Assert.Null(configObject.MainNetBitcoinCorePort);
			Assert.Equal("nico1:3929", configObject.TestNetBitcoinCoreHost);
			Assert.Null(configObject.TestNetBitcoinCorePort);
			Assert.Equal("nico2:22", configObject.RegTestBitcoinCoreHost);
			Assert.Null(configObject.RegTestBitcoinCorePort);
#pragma warning restore CS0612 // Type or member is obsolete
		}
	}
}
