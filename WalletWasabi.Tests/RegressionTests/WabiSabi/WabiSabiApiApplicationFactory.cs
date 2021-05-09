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
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public class WabiSabiApiApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
	{
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
				services.AddSingleton<ArenaRequestHandler>();
				services.AddScoped<Network>(_ => Network.Main);
				services.AddScoped<IRPCClient>(_ => BitcoinFactory.GetMockMinimalRpc());
				services.AddScoped<Prison>();
				services.AddScoped<WabiSabiConfig>();
				services.AddScoped(typeof(TimeSpan), _ => TimeSpan.FromSeconds(2));
			});
		}

		public async Task<ArenaClient> CreateArenaClientAsync(HttpClient? httpClient = null)
		{
			var wabiSabiHttpApiClient = CreateWabiSabiHttpApiClient(httpClient);
			var rounds = await wabiSabiHttpApiClient.GetStatusAsync(CancellationToken.None);
			var round = rounds.First(x => x.CoinjoinState is ConstructionState);
			var arenaClient = new ArenaClient(
				round.AmountCredentialIssuerParameters,
				round.VsizeCredentialIssuerParameters,
				new CredentialPool(),
				new CredentialPool(),
				wabiSabiHttpApiClient,
				new InsecureRandom());
			return arenaClient;
		}

		public WabiSabiHttpApiClient CreateWabiSabiHttpApiClient(HttpClient? httpClient = null) =>
			new(new HttpClientWrapper(httpClient ?? CreateClient()));
	}
}
