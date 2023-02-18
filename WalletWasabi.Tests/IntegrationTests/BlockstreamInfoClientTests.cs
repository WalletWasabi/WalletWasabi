using System.Net.Http;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.WabiSabi.Backend.WebClients;
using WalletWasabi.WebClients.BlockstreamInfo;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class BlockstreamInfoClientTests : IAsyncLifetime
{
	public BlockstreamInfoClientTests()
	{
		ClearnetHttpClientFactory = new(torEndPoint: null, backendUriGetter: null);
		TorHttpClientFactory = new(Common.TorSocks5Endpoint, backendUriGetter: null);

		TorManager = new(Common.TorSettings);
	}

	private HttpClientFactory ClearnetHttpClientFactory { get; }
	private HttpClientFactory TorHttpClientFactory { get; }
	private TorProcessManager TorManager { get; }

	public async Task InitializeAsync()
	{
		await TorManager.StartAsync();
	}

	public async Task DisposeAsync()
	{
		await ClearnetHttpClientFactory.DisposeAsync();
		await TorHttpClientFactory.DisposeAsync();
		await TorManager.DisposeAsync();
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
	public async Task GetTransactionStatusClearnetMainnetBackendAsync()
	{
		HttpClient httpClient = new();
		BlockstreamApiClient client = new(Network.Main, httpClient);

		// This TX exists
		uint256 txid = uint256.Parse("8a6edaae0ed93cf1a84fe727450be383ce53133df1a4438f9b9201b563ea9880");
		var status = await client.GetTransactionStatusAsync(txid, CancellationToken.None);
		Assert.NotNull(status);
		Assert.True(status);

		// This TX does not exist
		txid = uint256.Parse("8a6edaae0ed93cf1a84fe737450be383ce53133df1a4438f9b9201aaaaaaaaaa");
		status = await client.GetTransactionStatusAsync(txid, CancellationToken.None);
		Assert.Null(status);
		httpClient.Dispose();
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
