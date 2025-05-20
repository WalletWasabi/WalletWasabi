using System.Net.Http;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.WebClients.BlockstreamInfo;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class BlockstreamInfoClientTests : IAsyncLifetime
{
	public BlockstreamInfoClientTests()
	{
		ClearnetHttpClientFactory = new HttpClientFactory();
		TorHttpClientFactory = new OnionHttpClientFactory(Common.TorSocks5Endpoint.ToUri("socks5"));

		TorProcessManager = new(Common.TorSettings, new EventBus());
	}

	private IHttpClientFactory ClearnetHttpClientFactory { get; }
	private IHttpClientFactory TorHttpClientFactory { get; }
	private TorProcessManager TorProcessManager { get; }

	public async Task InitializeAsync()
	{
		using CancellationTokenSource startTimeoutCts = new(TimeSpan.FromMinutes(2));

		await TorProcessManager.StartAsync(startTimeoutCts.Token);
	}

	public async Task DisposeAsync()
	{
		await TorProcessManager.DisposeAsync();
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
