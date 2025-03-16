using NBitcoin;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
		HttpClientFactory = new OnionHttpClientFactory(new Uri($"socks5://{Common.TorSocks5Endpoint}"));
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

		WasabiClient client = MakeWasabiClient(network);
		var backendMajorVersion = await client.GetBackendMajorVersionAsync(ctsTimeout.Token);
		Assert.Equal(4, backendMajorVersion);
	}

	private WasabiClient MakeWasabiClient(Network network)
	{
		HttpClient httpClient = HttpClientFactory.CreateClient();
		httpClient.BaseAddress =LiveServerTestsFixture.UriMappings[network];
		return new WasabiClient(httpClient);
	}

	public static IEnumerable<object[]> GetNetworks()
	{
		yield return new object[] { Network.Main };
		yield return new object[] { Network.TestNet };
	}
}
