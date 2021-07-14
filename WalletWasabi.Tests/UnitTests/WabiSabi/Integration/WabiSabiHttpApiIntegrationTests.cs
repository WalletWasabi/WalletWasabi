using System;
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
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
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
			   await apiClient.RegisterInputAsync(round.Id, nonExistingOutPoint, signingKey, CancellationToken.None));

			var wex = Assert.IsType<WabiSabiProtocolException>(ex.InnerException);
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, wex.ErrorCode);
		}

		[Theory]
		[InlineData(new long[] { 20_000_000, 40_000_000, 60_000_000, 80_000_000 })]
		[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 })]
		[InlineData(new long[] { 120_000_000 })]
		[InlineData(new long[] { 100_000_000, 10_000_000, 10_000 })]
		public async Task SoloCoinJoinTestAsync(long[] amounts)
		{
			int inputCount = amounts.Length;

			// At the end of the test a coinjoin transaction has to be created and broadcasted.
			var transactionCompleted = new TaskCompletionSource<Transaction>();

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
			cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

			// Create a key manager and use it to create fake coins.
			var keyManager = KeyManager.CreateNew(out var _, password: "");
			keyManager.AssertCleanKeysIndexed();
			var coins = keyManager.GetKeys()
				.Take(inputCount)
				.Select((x, i) => new Coin(
					BitcoinFactory.CreateOutPoint(),
					new TxOut(Money.Satoshis(amounts[i]), x.P2wpkhScript)))
				.ToArray();

			var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					var rpc = BitcoinFactory.GetMockMinimalRpc();

					// Make the coordinator to believe that the coins are real and
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
					services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig { MaxInputCountByRound = inputCount });
				});
			}).CreateClient();

			// Create the coinjoin client
			var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);
			await roundStateUpdater.StartAsync(CancellationToken.None);

			var kitchen = new Kitchen();
			kitchen.Cook("");

			var coinJoinClient = new CoinJoinClient(apiClient, coins, kitchen, keyManager, roundStateUpdater);

			// Run the coinjoin client task.
			Assert.True(await coinJoinClient.StartCoinJoinAsync(cts.Token));

			var broadcastedTx = await transactionCompleted.Task.ConfigureAwait(false); // wait for the transaction to be broadcasted.
			Assert.NotNull(broadcastedTx);

			await roundStateUpdater.StopAsync(CancellationToken.None);
		}

		[Theory]
		[InlineData(new long[] { 20_000_000, 40_000_000, 60_000_000, 80_000_000 })]
		[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 })]
		[InlineData(new long[] { 100_000_000, 10_000_000, 10_000 })]
		public async Task SoloCoinJoinWithBlameRoundTestAsync(long[] amounts)
		{
			int inputCount = amounts.Length;

			// At the end of the test a coinjoin transaction has to be created and broadcasted.
			var transactionCompleted = new TaskCompletionSource<Transaction>();

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
			cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

			var keyManager1 = KeyManager.CreateNew(out var _, password: "");
			keyManager1.AssertCleanKeysIndexed();

			var keyManager2 = KeyManager.CreateNew(out var _, password: "");
			keyManager2.AssertCleanKeysIndexed();

			var coins = keyManager1.GetKeys()
				.Take(inputCount)
				.Select((x, i) => new Coin(
					BitcoinFactory.CreateOutPoint(),
					new TxOut(Money.Satoshis(amounts[i]), x.P2wpkhScript)))
				.ToArray();

			var badCoins = keyManager2.GetKeys()
				.Take(inputCount)
				.Select((x, i) => new Coin(
							BitcoinFactory.CreateOutPoint(),
							new TxOut(Money.Satoshis(amounts[i]), x.P2wpkhScript)))
				.ToArray();

			var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					var rpc = BitcoinFactory.GetMockMinimalRpc();

					// Make the coordinator to believe that the coins are real and
					// that they exist in the blockchain with many confirmations.
					rpc.OnGetTxOutAsync = (txId, idx, _) => new()
					{
						Confirmations = 101,
						IsCoinBase = false,
						ScriptPubKeyType = "witness_v0_keyhash",
						TxOut = Enumerable.Concat(coins, badCoins).Single(x => x.Outpoint.Hash == txId && x.Outpoint.N == idx).TxOut
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
					services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig { MaxInputCountByRound = 2 * inputCount });
				});
			}).CreateClient();

			// Create the coinjoin client
			var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);
			await roundStateUpdater.StartAsync(CancellationToken.None);

			var roundState = await roundStateUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cts.Token);

			var kitchen = new Kitchen();
			kitchen.Cook("");

			var coinJoinClient = new CoinJoinClient(apiClient, coins, kitchen, keyManager1, roundStateUpdater);

			// Run the coinjoin client task.
			var coinJoinTask = Task.Run(async () => await coinJoinClient.StartCoinJoinAsync(cts.Token));

			var noSignatureApiClient = new SignatureDroppingClient(apiClient);
			var badCoinJoinClient = new CoinJoinClient(noSignatureApiClient, badCoins, kitchen, keyManager2, roundStateUpdater);
			var badCoinsTask = Task.Run(async () => await badCoinJoinClient.StartRoundAsync(roundState, cts.Token) );

			await Task.WhenAll(new Task[] { badCoinsTask, coinJoinTask });

			Assert.False(badCoinsTask.Result);
			Assert.True(coinJoinTask.Result);

			var broadcastedTx = await transactionCompleted.Task.ConfigureAwait(false); // wait for the transaction to be broadcasted.
			Assert.NotNull(broadcastedTx);

			Assert.Equal(
				coins.Select(x => x.Outpoint.ToString()).OrderBy(x => x),
				broadcastedTx.Inputs.Select(x => x.PrevOut.ToString()).OrderBy(x => x));

			await roundStateUpdater.StopAsync(CancellationToken.None);
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

			var response = await apiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, signingKey, CancellationToken.None);

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

			var response = await apiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, signingKey, CancellationToken.None);

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

		private class SignatureDroppingClient : IWabiSabiApiRequestHandler
		{
			private IWabiSabiApiRequestHandler ApiClient;

			public SignatureDroppingClient(IWabiSabiApiRequestHandler apiClient)
			{
				ApiClient = apiClient;
			}

			public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
			{
				return;
			}

			public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
				=> ApiClient.RegisterInputAsync(request, cancellationToken);
			public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
				=> ApiClient.ConfirmConnectionAsync(request, cancellationToken);
			public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
				=> ApiClient.RegisterOutputAsync(request, cancellationToken);
			public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
				=> ApiClient.ReissueCredentialAsync(request, cancellationToken);
			public Task ReadyToSign(ReadyToSignRequestRequest request, CancellationToken cancellationToken) // FIXME naming
				=> ApiClient.ReadyToSign(request, cancellationToken);
			public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
				=> ApiClient.RemoveInputAsync(request, cancellationToken);
			public Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
				=> ApiClient.GetStatusAsync(cancellationToken);
		}
	}
}
