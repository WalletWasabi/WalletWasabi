using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public class WabiSabiApiApplicationFactory : WebApplicationFactory<WalletWasabi.Backend.Startup>
	{
		public WabiSabiApiApplicationFactory()
			: base()
		{
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			// will be called after the `ConfigureServices` from the Startup
			builder.ConfigureTestServices(services =>
			{
				services.AddSingleton<IRPCClient>(GetMockRpc());
				services.AddTransient<IArenaRequestHandler, ArenaRequestHandler>();
				services.AddTransient<Prison>();
				services.AddTransient<WabiSabiConfig>();
				services.AddSingleton<Arena>(serviceProvider => 
				{
					var rpc = serviceProvider.GetRequiredService<IRPCClient>();
					var prison = serviceProvider.GetRequiredService<Prison>();
					var config = serviceProvider.GetRequiredService<WabiSabiConfig>();
					var arena = new Arena(TimeSpan.FromSeconds(4), Network.Main, config, rpc, prison);
					arena.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
					arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
					return arena;
				});
			});
		}

		public ArenaClient CreateArenaClient(HttpClient? httpClient = null)
		{
			httpClient ??= CreateClient();
			var httpArenaRequestHandlerProxy = new WabiSabiHttpApiClient(new HttpClientWrapper(httpClient));
			var round = GetCurrentRound();
			var arenaClient = new ArenaClient(
				round.AmountCredentialIssuerParameters, 
				round.VsizeCredentialIssuerParameters,
				new CredentialPool(),
				new CredentialPool(),
				httpArenaRequestHandlerProxy, 
				new InsecureRandom());
			return arenaClient;
		}

		public Round GetCurrentRound()
		{
			var arena = Services.GetRequiredService<Arena>();
			var round = arena.Rounds.First().Value;
			return round;
		}

		private MockRpcClient GetMockRpc()
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
}
