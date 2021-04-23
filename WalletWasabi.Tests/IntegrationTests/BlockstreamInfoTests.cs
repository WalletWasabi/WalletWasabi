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
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class BlockstreamInfoTests : IAsyncLifetime
	{
		public BlockstreamInfoTests()
		{
			ClearnetHttpClientFactory = new(torEndPoint: null, backendUriGetter: null);
			TorHttpClientFactory = new(Common.TorSocks5Endpoint, backendUriGetter: null);

			TorManager = new(Common.TorSettings, Common.TorSocks5Endpoint);
		}

		private HttpClientFactory ClearnetHttpClientFactory { get; }
		private HttpClientFactory TorHttpClientFactory { get; }
		private TorProcessManager TorManager { get; }

		public async Task InitializeAsync()
		{
			bool started = await TorManager.StartAsync();
			Assert.True(started, "Tor failed to start.");
		}

		public Task DisposeAsync()
		{
			return Task.CompletedTask;
		}

		[Fact]
		public async Task GetFeeEstimatesClearnetMainnetAsync()
		{
			BlockstreamInfoClient client = new(Network.Main, ClearnetHttpClientFactory);
			AllFeeEstimate estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesTorMainnetAsync()
		{
			Common.GetWorkDir();
			Logging.Logger.SetMinimumLevel(Logging.LogLevel.Trace);

			BlockstreamInfoClient client = new(Network.Main, TorHttpClientFactory);
			AllFeeEstimate estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesClearnetTestnetAsync()
		{
			BlockstreamInfoClient client = new(Network.TestNet, ClearnetHttpClientFactory);
			AllFeeEstimate estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task GetFeeEstimatesTorTestnetAsync()
		{
			BlockstreamInfoClient client = new(Network.TestNet, TorHttpClientFactory);
			AllFeeEstimate estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task SimulatesFeeEstimatesClearnetRegtestAsync()
		{
			BlockstreamInfoClient client = new(Network.RegTest, ClearnetHttpClientFactory);
			AllFeeEstimate estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}

		[Fact]
		public async Task SimulatesFeeEstimatesTorRegtestAsync()
		{
			BlockstreamInfoClient client = new(Network.RegTest, TorHttpClientFactory);
			AllFeeEstimate estimates = await client.GetFeeEstimatesAsync(CancellationToken.None);
			Assert.NotNull(estimates);
			Assert.NotEmpty(estimates.Estimations);
		}
	}
}
