using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.WebClients.BlockstreamInfo;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class BlockstreamInfoTests
	{
		[Fact]
		public async Task GetFeeEstimatesMainnetAsync()
		{
			using var client = new BlockstreamInfoClient(Network.Main);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesTestnetAsync()
		{
			using var client = new BlockstreamInfoClient(Network.TestNet);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task SimulatesFeeEstimatesRegtestAsync()
		{
			using var client = new BlockstreamInfoClient(Network.RegTest);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}
	}
}
