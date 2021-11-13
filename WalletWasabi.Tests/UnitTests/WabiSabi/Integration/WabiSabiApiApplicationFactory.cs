using System;
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
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration
{
	public class WabiSabiApiApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
	{
		// There is a deadlock in the current version of the asmp.net testing framework
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
			var builder = Host.CreateDefaultBuilder().ConfigureWebHostDefaults(x =>
			{
				x.UseStartup<TStartup>().UseTestServer();
			});
			return builder;
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			// will be called after the `ConfigureServices` from the Startup
			builder.ConfigureTestServices(services =>
			{
				services.AddHostedService<BackgroundServiceStarter<Arena>>();
				services.AddSingleton<Arena>();
				services.AddScoped<Network>(_ => Network.Main);
				services.AddScoped<IRPCClient>(_ => BitcoinFactory.GetMockMinimalRpc());
				services.AddScoped<Prison>();
				services.AddScoped<WabiSabiConfig>();
				services.AddScoped(typeof(TimeSpan), _ => TimeSpan.FromSeconds(2));
			});
			builder.ConfigureLogging(o =>
			{
				o.SetMinimumLevel(LogLevel.Warning);
			});
		}

		public Task<ArenaClient> CreateArenaClientAsync(HttpClient httpClient) =>
			CreateArenaClientAsync(CreateWabiSabiHttpApiClient(httpClient));

		public Task<ArenaClient> CreateArenaClientAsync(IHttpClient httpClient) =>
			CreateArenaClientAsync(new WabiSabiHttpApiClient(httpClient));

		public async Task<ArenaClient> CreateArenaClientAsync(WabiSabiHttpApiClient wabiSabiHttpApiClient)
		{
			var rounds = await wabiSabiHttpApiClient.GetStatusAsync(CancellationToken.None);
			var round = rounds.First(x => x.CoinjoinState is ConstructionState);
			var insecureRandom = new InsecureRandom();
			var arenaClient = new ArenaClient(
				round.CreateAmountCredentialClient(insecureRandom),
				round.CreateVsizeCredentialClient(insecureRandom),
				wabiSabiHttpApiClient);
			return arenaClient;
		}

		public WabiSabiHttpApiClient CreateWabiSabiHttpApiClient(HttpClient httpClient) =>
			new(new HttpClientWrapper(httpClient));
	}
}
