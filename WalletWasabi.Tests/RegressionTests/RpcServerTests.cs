using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests
{
	[Collection("RegTest collection")]
	public class RpcServerTests
	{
		private RegTestFixture RegTestFixture { get; }

		public RpcServerTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
		}

		[Fact]
		public async Task GetStatusTestAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
			WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);

			try
			{
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 1000);
			}
			finally
			{
				if (synchronizer is { })
				{
					await synchronizer.StopAsync();
				}
			}
		}
	}
}
