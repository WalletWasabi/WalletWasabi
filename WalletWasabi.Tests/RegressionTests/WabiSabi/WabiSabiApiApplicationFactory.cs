using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests;
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
				services.AddScoped<IRPCClient>(_ => GetMockRpc());
				services.AddScoped<Prison>();
				services.AddScoped<WabiSabiConfig>();
				services.AddScoped(typeof(TimeSpan), _ => TimeSpan.FromSeconds(2));

				/*
				serviceProvider =>
				{
					var rpc = serviceProvider.GetRequiredService<IRPCClient>();
					var prison = serviceProvider.GetRequiredService<Prison>();
					var config = serviceProvider.GetRequiredService<WabiSabiConfig>();
					var arena = new Arena(TimeSpan.FromSeconds(4), Network.Main, config, rpc, prison);
					arena.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
					arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
					return arena;
				});*/
			});
			builder.ConfigureServices(services =>
			{
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

		// Creates and configure an fake RPC client used to simulate the
		// interaction with our bitcoin full node RPC server.
		private static MockRpcClient GetMockRpc()
		{
			var mockRpc = new MockRpcClient();
			mockRpc.OnGetMempoolInfoAsync = () => Task.FromResult(
				new MemPoolInfo
				{
					MemPoolMinFee = 0.00001000, // 1 s/b (default value)
					Histogram = Array.Empty<FeeRateGroup>()
				});

			mockRpc.OnEstimateSmartFeeAsync = (target, mode) => Task.FromResult(
				new EstimateSmartFeeResponse()
				{
					Blocks = target,
					FeeRate = new FeeRate(Money.Satoshis(5000))
				});

			mockRpc.OnGetTxOutAsync = (_, _, _) => null;

			return mockRpc;
		}
	}

	public class BackgroundServiceStarter<T> : IHostedService where T : IHostedService
	{
		readonly T backgroundService;

		public BackgroundServiceStarter(T backgroundService)
		{
			this.backgroundService = backgroundService;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			return backgroundService.StartAsync(cancellationToken);
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return backgroundService.StopAsync(cancellationToken);
		}
	}
}
