using NBitcoin;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

[Collection("LiveServerTests collection")]
public class LiveServerTests : IAsyncLifetime
{
	public LiveServerTests(LiveServerTestsFixture liveServerTestsFixture)
	{
		LiveServerTestsFixture = liveServerTestsFixture;
		HttpClientFactory = new OnionHttpClientFactory(Common.TorSocks5Endpoint.ToUri("socks5"));
		TorProcessManager = new(Common.TorSettings, new EventBus());
	}

	private TorProcessManager TorProcessManager { get; }
	private IHttpClientFactory HttpClientFactory { get; }
	private LiveServerTestsFixture LiveServerTestsFixture { get; }

	public async Task InitializeAsync()
	{
		using CancellationTokenSource startTimeoutCts = new(TimeSpan.FromMinutes(2));

		await TorProcessManager.StartAsync(startTimeoutCts.Token);
	}

	public async Task DisposeAsync()
	{
		await TorProcessManager.DisposeAsync();
	}

	[Theory]
	[MemberData(nameof(GetNetworks))]
	public async Task GetBackendVersionTestsAsync(Network network)
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		IndexerClient client = MakeIndexerClient(network);
		var backendMajorVersion = await client.GetIndexerMajorVersionAsync(ctsTimeout.Token);
		Assert.Equal(4, backendMajorVersion);
	}

	private IndexerClient MakeIndexerClient(Network network)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope - HttpClient ownership transferred to IndexerClient
		HttpClient httpClient = HttpClientFactory.CreateClient();
#pragma warning restore CA2000
		httpClient.BaseAddress =LiveServerTestsFixture.UriMappings[network];
		return new IndexerClient(httpClient);
	}

	public static IEnumerable<object[]> GetNetworks()
	{
		yield return new object[] { Network.Main };
		yield return new object[] { Network.TestNet };
	}
}
