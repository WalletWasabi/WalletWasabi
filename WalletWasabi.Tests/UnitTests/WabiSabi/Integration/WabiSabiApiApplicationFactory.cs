using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Coordinator.Statistics;
using Arena = WalletWasabi.WabiSabi.Coordinator.Rounds.Arena;
using WalletWasabi.Tests.UnitTests.Mocks;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

public class WabiSabiApiApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
	// There is a deadlock in the current version of the asp.net testing framework
	// https://www.strathweb.com/2021/05/the-curious-case-of-asp-net-core-integration-test-deadlock/
	protected override IHost CreateHost(IHostBuilder builder)
	{
		var host = builder.Build();
		Task.Run(() => host.StartAsync()).GetAwaiter().GetResult();
		return host;
	}

	protected override void ConfigureClient(HttpClient client)
	{
		client.Timeout = TimeSpan.FromMinutes(10);
	}

	protected override IHostBuilder CreateHostBuilder()
	{
		var builder = Host.CreateDefaultBuilder().ConfigureWebHostDefaults(x => x.UseStartup<TStartup>().UseTestServer());
		return builder;
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// will be called after the `ConfigureServices` from the Startup
		builder.ConfigureTestServices(services =>
		{
			services.AddHostedService<BackgroundServiceStarter<Arena>>();
			services.AddSingleton<Arena>();
			services.AddSingleton(_ => Network.RegTest);
			services.AddSingleton<IRPCClient>(_ => BitcoinFactory.GetMockMinimalRpc());
			services.AddSingleton<Prison>(_ => WabiSabiFactory.CreatePrison());
			services.AddSingleton<WabiSabiConfig>();
			services.AddSingleton<RoundParameterFactory>();
			services.AddSingleton(typeof(TimeSpan), _ => TimeSpan.FromSeconds(2));
			services.AddSingleton(s => new CoinJoinScriptStore());
			services.AddSingleton(s => FeeRateProviders.RpcAsync(s.GetRequiredService<IRPCClient>()));
			services.AddHttpClient();
		});
		builder.ConfigureLogging(o => o.SetMinimumLevel(LogLevel.Warning));
	}

	public Task<ArenaClient> CreateArenaClientAsync(HttpClient httpClient) =>
		CreateArenaClientAsync(CreateWabiSabiHttpApiClient(httpClient));

	public async Task<ArenaClient> CreateArenaClientAsync(WabiSabiHttpApiClient wabiSabiHttpApiClient)
	{
		var rounds = (await wabiSabiHttpApiClient.GetStatusAsync(RoundStateRequest.Empty, CancellationToken.None)).RoundStates;
		var round = rounds.First(x => x.Phase is Phase.InputRegistration or Phase.OutputRegistration or Phase.ConnectionConfirmation);
		var arenaClient = new ArenaClient(
			round.CreateAmountCredentialClient(InsecureRandom.Instance),
			round.CreateVsizeCredentialClient(InsecureRandom.Instance),
			round.CoinjoinState.Parameters.CoordinationIdentifier,
			wabiSabiHttpApiClient);
		return arenaClient;
	}

	public WabiSabiHttpApiClient CreateWabiSabiHttpApiClient(HttpClient httpClient) =>
		new("identity", new MockHttpClientFactory { OnCreateClient = _ => httpClient});
}
