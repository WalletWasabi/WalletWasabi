using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.WebClients.BlockstreamInfo;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class BlockstreamInfoTests : IAsyncLifetime
	{
		public BlockstreamInfoTests()
		{
			TorHttpPool = new(new TorTcpConnectionFactory(Common.TorSocks5Endpoint));
			TorManager = new(Common.TorSettings, Common.TorSocks5Endpoint);
		}

		private TorHttpPool TorHttpPool { get; }
		private TorProcessManager TorManager { get; }

		public async Task InitializeAsync()
		{
			bool started = await TorManager.StartAsync(ensureRunning: true);
			Assert.True(started, "Tor failed to start.");
		}

		public Task DisposeAsync()
		{
			return Task.CompletedTask;
		}

		[Fact]
		public async Task GetFeeEstimatesClearnetMainnetAsync()
		{
			using var client = new BlockstreamInfoClient(Network.Main);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesTorMainnetAsync()
		{
			using var client = new BlockstreamInfoClient(Network.Main, TorHttpPool);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesClearnetTestnetAsync()
		{
			using var client = new BlockstreamInfoClient(Network.TestNet);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesTorTestnetAsync()
		{
			using var client = new BlockstreamInfoClient(Network.TestNet, TorHttpPool);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task SimulatesFeeEstimatesClearnetRegtestAsync()
		{
			using var client = new BlockstreamInfoClient(Network.RegTest);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task SimulatesFeeEstimatesTorRegtestAsync()
		{
			using var client = new BlockstreamInfoClient(Network.RegTest, TorHttpPool);
			var estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}
	}
}
