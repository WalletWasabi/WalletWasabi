using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public class WabiSabiHttpApiIntegrationTests : IClassFixture<WabiSabiApiApplicationFactory<Startup>>
	{
		private readonly WabiSabiApiApplicationFactory<Startup> _apiApplicationFactory;

		public WabiSabiHttpApiIntegrationTests(WabiSabiApiApplicationFactory<Startup> apiApplicationFactory)
		{
			_apiApplicationFactory = apiApplicationFactory;
		}

		[Fact]
		public async Task RegisterSpentOrInNonExistentCoinAsync()
		{
			var httpClient = _apiApplicationFactory.CreateClient();

			var apiClient = await _apiApplicationFactory.CreateArenaClientAsync();
			var rounds = await apiClient.GetStatusAsync(CancellationToken.None);
			var round = rounds.First(x => x.CoinjoinState is ConstructionState);
			// If an output is not in the utxo dataset then it is not unspent, this
			// means that the output is spent or simply doesn't even exist.
			var nonExistingOutPoint = new OutPoint();
			using var signingKey = new Key();

			var ex = await Assert.ThrowsAsync<HttpRequestException>( async () =>
				await apiClient.RegisterInputAsync(Money.Coins(1), nonExistingOutPoint, signingKey, round.Id, CancellationToken.None));

			var wex = Assert.IsType<WabiSabiProtocolException>(ex.InnerException);
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, wex.ErrorCode);
		}

		[Fact]
		public async Task RegisterCoinAsync()
		{
			using var signingKey = new Key();
			var coinToRegister = new Coin(
				BitcoinFactory.CreateOutPoint(),
				new TxOut(Money.Coins(1), signingKey.PubKey.WitHash.ScriptPubKey));

			var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					var rpc = GetMockRpc();
					rpc.OnGetTxOutAsync = (_, _, _) => new ()
					{
						Confirmations = 101,
						IsCoinBase = false,
						ScriptPubKeyType = "witness_v0_keyhash",
						TxOut = coinToRegister.TxOut
					};
					services.AddScoped<IRPCClient>(s => rpc);
				});
			}).CreateClient();

			var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
			var rounds = await apiClient.GetStatusAsync(CancellationToken.None);
			var round = rounds.First(x => x.CoinjoinState is ConstructionState);

			uint256 aliceId = await apiClient.RegisterInputAsync(Money.Coins(1), coinToRegister.Outpoint, signingKey, round.Id, CancellationToken.None);

			Assert.NotEqual(uint256.Zero, aliceId);
		}

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
}
