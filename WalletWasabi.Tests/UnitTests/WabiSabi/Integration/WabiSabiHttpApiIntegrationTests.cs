using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration
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

			var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
			var rounds = await apiClient.GetStatusAsync(CancellationToken.None);
			var round = rounds.First(x => x.CoinjoinState is ConstructionState);

			// If an output is not in the utxo dataset then it is not unspent, this
			// means that the output is spent or simply doesn't even exist.
			var nonExistingOutPoint = new OutPoint();
			using var signingKey = new Key();

			var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
			   await apiClient.RegisterInputAsync(nonExistingOutPoint, signingKey, round.Id, CancellationToken.None));

			var wex = Assert.IsType<WabiSabiProtocolException>(ex.InnerException);
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, wex.ErrorCode);
		}

		[Fact]
		public async Task SoloCoinJoinTest()
		{
			const int InputCount = 2;

			// At the end of the test a coinjoin transaction has to be created and broadcasted.
			// we wait for 20 seconds before timing out.
			const int CoinjoinProcessTimeout = 40_000;
			var transactionCompleted = new TaskCompletionSource<Transaction>();
			using var cts = new CancellationTokenSource(CoinjoinProcessTimeout);
			cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

			// Create a key manager and use it to create two fake coins.
			var keyManager = KeyManager.CreateNew(out var _, password: "");
			keyManager.AssertCleanKeysIndexed();
			var coins = keyManager.GetKeys()
				.Take(InputCount)
				.Select(x => new Coin(
					BitcoinFactory.CreateOutPoint(),
					new TxOut(Money.Coins(1), x.P2wpkhScript)))
				.ToArray();

			var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					var rpc = BitcoinFactory.GetMockMinimalRpc();

					// Make the coordinator to believe that those two coins are real and
					// that they exist in the blockchain with many confirmations.
					rpc.OnGetTxOutAsync = (txId, idx, _) => new()
					{
						Confirmations = 101,
						IsCoinBase = false,
						ScriptPubKeyType = "witness_v0_keyhash",
						TxOut = coins.Single(x => x.Outpoint.Hash == txId && x.Outpoint.N == idx).TxOut
					};

					// Make the coordinator believe that the transaction is being
					// broadcasted using the RPC interface. Once we receive this tx
					// (the `SendRawTransationAsync` was invoked) we stop waiting
					// and finish the waiting tasks to finish the test successfully.
					rpc.OnSendRawTransactionAsync = (tx) =>
					{
						transactionCompleted.SetResult(tx);
						return tx.GetHash();
					};

					// Instruct the coodinator DI container to use these two scoped
					// services to build everything (wabisabi controller, arena, etc)
					services.AddScoped<IRPCClient>(s => rpc);
					services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig { MaxInputCountByRound = InputCount });
				});
			}).CreateClient();

			// Create the coinjoin client
			var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
			var rounds = await apiClient.GetStatusAsync(CancellationToken.None);
			var roundState = rounds.First(x => x.CoinjoinState is ConstructionState);
			var kitchen = new Kitchen();
			kitchen.Cook("");
			using var coinJoinClient = new CoinJoinClient(roundState.Id, apiClient, coins, kitchen, keyManager);

			// Run the coinjoin client task.
			await coinJoinClient.StartAsync(CancellationToken.None);

			var boadcastedTx = await transactionCompleted.Task.ConfigureAwait(false); // wait for the transaction to be broadcasted.
			Assert.NotNull(boadcastedTx);

			await coinJoinClient.StopAsync(CancellationToken.None);
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
					var rpc = BitcoinFactory.GetMockMinimalRpc();
					rpc.OnGetTxOutAsync = (_, _, _) => new()
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

			var response = await apiClient.RegisterInputAsync(coinToRegister.Outpoint, signingKey, round.Id, CancellationToken.None);

			Assert.NotEqual(uint256.Zero, response.Value);
		}

		[Fact]
		public async Task RegisterCoinIdempotencyAsync()
		{
			using var signingKey = new Key();
			var coinToRegister = new Coin(
				BitcoinFactory.CreateOutPoint(),
				new TxOut(Money.Coins(1), signingKey.PubKey.WitHash.ScriptPubKey));

			var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					var rpc = BitcoinFactory.GetMockMinimalRpc();
					rpc.OnGetTxOutAsync = (_, _, _) => new()
					{
						Confirmations = 101,
						IsCoinBase = false,
						ScriptPubKeyType = "witness_v0_keyhash",
						TxOut = coinToRegister.TxOut
					};
					services.AddScoped<IRPCClient>(s => rpc);
				});
			}).CreateClient();

			var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(new StuttererHttpClient(httpClient));
			var rounds = await apiClient.GetStatusAsync(CancellationToken.None);
			var round = rounds.First(x => x.CoinjoinState is ConstructionState);

			var response = await apiClient.RegisterInputAsync(coinToRegister.Outpoint, signingKey, round.Id, CancellationToken.None);

			Assert.NotEqual(uint256.Zero, response.Value);
		}

		private class StuttererHttpClient : HttpClientWrapper
		{
			public StuttererHttpClient(HttpClient httpClient) : base(httpClient)
			{
			}

			public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
			{
				var result1 = await base.SendAsync(request.Clone(), token);
				var result2 = await base.SendAsync(request.Clone(), token);
				var content1 = await result1.Content.ReadAsStringAsync();
				var content2 = await result2.Content.ReadAsStringAsync();
				Assert.Equal(content1, content2);
				return result2;
			}
		}
	}
}
