using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
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
			var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);

			var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
			   await apiClient.RegisterInputAsync(round.Id, nonExistingOutPoint, ownershipProof, CancellationToken.None));

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
			var keyManager = KeyManager.CreateNew(out var _, password: "", Network.Main);
			keyManager.AssertCleanKeysIndexed();
			var coins = keyManager.GetKeys()
				.Take(inputCount)
				.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
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
						TxOut = coins.Single(x => x.TransactionId == txId && x.Index == idx).TxOut
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

					// Instruct the coordinator DI container to use these two scoped
					// services to build everything (WabiSabi controller, arena, etc)
					services.AddScoped<IRPCClient>(s => rpc);
					services.AddScoped(s => new WabiSabiConfig
					{
						MaxInputCountByRound = inputCount,
						StandardInputRegistrationTimeout = TimeSpan.FromSeconds(10)
					});
				});
			}).CreateClient();

			// Create the coinjoin client
			var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
			var mockHttpClientFactory = new Mock<IBackendHttpClientFactory>();
			mockHttpClientFactory
				.Setup(factory => factory.NewBackendHttpClient(It.IsAny<Mode>()))
				.Returns(new HttpClientWrapper(httpClient));

			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);
			await roundStateUpdater.StartAsync(CancellationToken.None);

			var kitchen = new Kitchen();
			kitchen.Cook("");

			var coinJoinClient = new CoinJoinClient(mockHttpClientFactory.Object, kitchen, keyManager, roundStateUpdater);

			// Run the coinjoin client task.
			Assert.True(await coinJoinClient.StartCoinJoinAsync(coins, cts.Token));

			var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
			Assert.NotNull(broadcastedTx);

			await roundStateUpdater.StopAsync(CancellationToken.None);
		}

		[Theory]
		[InlineData(new long[] { 20_000_000, 40_000_000, 60_000_000, 80_000_000 })]
		[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 })]
		[InlineData(new long[] { 100_000_000, 10_000_000, 10_000 })]
		public async Task CoinJoinWithBlameRoundTestAsync(long[] amounts)
		{
			int inputCount = amounts.Length;

			// At the end of the test a coinjoin transaction has to be created and broadcasted.
			var transactionCompleted = new TaskCompletionSource<Transaction>(TaskCreationOptions.RunContinuationsAsynchronously);

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
			cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

			var keyManager1 = KeyManager.CreateNew(out var _, password: "", Network.Main);
			keyManager1.AssertCleanKeysIndexed();

			var keyManager2 = KeyManager.CreateNew(out var _, password: "", Network.Main);
			keyManager2.AssertCleanKeysIndexed();

			var coins = keyManager1.GetKeys()
				.Take(inputCount)
				.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
				.ToArray();

			var badCoins = keyManager2.GetKeys()
				.Take(inputCount)
				.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
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
						TxOut = Enumerable.Concat(coins, badCoins).Single(x => x.TransactionId == txId && x.Index == idx).TxOut
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

					// Instruct the coordinator DI container to use these two scoped
					// services to build everything (WabiSabi controller, arena, etc)
					services.AddScoped<IRPCClient>(s => rpc);
					services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig
					{
						MaxInputCountByRound = 2 * inputCount,
						StandardInputRegistrationTimeout = TimeSpan.FromSeconds(20),
						BlameInputRegistrationTimeout = TimeSpan.FromSeconds(20),
						TransactionSigningTimeout = TimeSpan.FromSeconds(5 * inputCount),
					});
				});
			}).CreateClient();

			// Create the coinjoin client
			var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
			var mockHttpClientFactory = new Mock<IBackendHttpClientFactory>();
			mockHttpClientFactory
				.Setup(factory => factory.NewBackendHttpClient(It.IsAny<Mode>()))
				.Returns(new HttpClientWrapper(httpClient));

			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);
			await roundStateUpdater.StartAsync(CancellationToken.None);

			var roundState = await roundStateUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cts.Token);

			var kitchen = new Kitchen();
			kitchen.Cook("");

			var coinJoinClient = new CoinJoinClient(mockHttpClientFactory.Object, kitchen, keyManager1, roundStateUpdater);

			// Run the coinjoin client task.
			var coinJoinTask = Task.Run(async () => await coinJoinClient.StartCoinJoinAsync(coins, cts.Token).ConfigureAwait(false), cts.Token);

			// Creates a IBackendHttpClientFactory that creates an HttpClient that says everything is okay
			// when a signature is sent but it doesn't really send it.
			var nonSigningHttpClient = new Mock<HttpClientWrapper>(httpClient);
			nonSigningHttpClient
				.Setup(httpClient => httpClient.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
				.CallBase();
			nonSigningHttpClient
				.Setup(httpClient => httpClient.SendAsync(It.Is<HttpRequestMessage>(
					req => req.RequestUri!.AbsolutePath.Contains("transaction-signature")),
					It.IsAny<CancellationToken>()))
				.ThrowsAsync(new HttpRequestException("Something was wrong posting the signature."));

			var mockNonSigningHttpClientFactory = new Mock<IBackendHttpClientFactory>();
			mockNonSigningHttpClientFactory
				.Setup(factory => factory.NewBackendHttpClient(It.IsAny<Mode>()))
				.Returns(nonSigningHttpClient.Object);

			var badCoinJoinClient = new CoinJoinClient(mockNonSigningHttpClientFactory.Object, kitchen, keyManager2, roundStateUpdater);
			var badCoinsTask = Task.Run(async () => await badCoinJoinClient.StartRoundAsync(badCoins, roundState, cts.Token).ConfigureAwait(false), cts.Token);

			await Task.WhenAll(new Task[] { badCoinsTask, coinJoinTask });

			Assert.False(badCoinsTask.Result);
			Assert.True(coinJoinTask.Result);

			var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
			Assert.NotNull(broadcastedTx);

			Assert.Equal(
				coins.Select(x => x.Coin.Outpoint.ToString()).OrderBy(x => x),
				broadcastedTx.Inputs.Select(x => x.PrevOut.ToString()).OrderBy(x => x));

			await roundStateUpdater.StopAsync(CancellationToken.None);
		}

		[Theory]
		[InlineData(123456)]
		public async Task MultiClientsCoinJoinTestAsync(int seed)
		{
			const int NumberOfParticipants = 20;
			const int NumberOfCoinsPerParticipant = 2;
			const int ExpectedInputNumber = (NumberOfParticipants * NumberOfCoinsPerParticipant) / 2;

			var node = await TestNodeBuilder.CreateForHeavyConcurrencyAsync();
			try
			{
				var rpc = node.RpcClient;

				var app = _apiApplicationFactory.WithWebHostBuilder(builder =>
				{
					builder.ConfigureServices(services =>
					{
						// Instruct the coordinator DI container to use these two scoped
						// services to build everything (WabiSabi controller, arena, etc)
						services.AddScoped<IRPCClient>(s => rpc);
						services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig
						{
							MaxRegistrableAmount = Money.Coins(500m),
							MaxInputCountByRound = ExpectedInputNumber,
							StandardInputRegistrationTimeout = TimeSpan.FromSeconds(10 * ExpectedInputNumber),
							ConnectionConfirmationTimeout = TimeSpan.FromSeconds(20 * ExpectedInputNumber),
							OutputRegistrationTimeout = TimeSpan.FromSeconds(20 * ExpectedInputNumber),
						});
					});
				});

				var mockHttpClientFactory = new Mock<IBackendHttpClientFactory>();
				mockHttpClientFactory
					.Setup(factory => factory.NewBackendHttpClient(It.IsAny<Mode>()))
					.Returns(new HttpClientWrapper(app.CreateClient()));

				// Total test timeout.
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40 * ExpectedInputNumber));

				var participants = Enumerable
					.Range(0, NumberOfParticipants)
					.Select(_ => new Participant(rpc, mockHttpClientFactory.Object))
					.ToArray();

				foreach (var participant in participants)
				{
					await participant.GenerateSourceCoinAsync(cts.Token);
				}
				using var dummyKey = new Key();
				await rpc.GenerateToAddressAsync(101, dummyKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, rpc.Network));
				foreach (var participant in participants)
				{
					await participant.GenerateCoinsAsync(NumberOfCoinsPerParticipant, seed, cts.Token);
				}
				await rpc.GenerateToAddressAsync(101, dummyKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, rpc.Network));

				var tasks = participants.Select(x => x.StartParticipatingAsync(cts.Token)).ToArray();

				while ((await rpc.GetRawMempoolAsync()).Length == 0)
				{
					if (cts.IsCancellationRequested)
					{
						throw new TimeoutException("CoinJoin was not propagated.");
					}

					await Task.Delay(500, cts.Token);

					if (tasks.FirstOrDefault(t => t.IsFaulted)?.Exception is { } exc)
					{
						throw exc;
					}
				}
				var mempool = await rpc.GetRawMempoolAsync();
				var coinjoin = await rpc.GetRawTransactionAsync(mempool.Single());

				Assert.True(coinjoin.Outputs.Count >= ExpectedInputNumber);
				Assert.True(coinjoin.Inputs.Count == ExpectedInputNumber);
			}
			finally
			{
				await node.TryStopAsync();
			}
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

			var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);
			var response = await apiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, ownershipProof, CancellationToken.None);

			Assert.NotEqual(Guid.Empty, response.Value);
		}

		[Fact]
		public async Task RegisterCoinIdempotencyAsync()
		{
			using Key signingKey = new();
			Coin coinToRegister = new(
				fromOutpoint: BitcoinFactory.CreateOutPoint(),
				fromTxOut: new TxOut(Money.Coins(1), signingKey.PubKey.WitHash.ScriptPubKey));

			using HttpClient httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
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

			ArenaClient apiClient = await _apiApplicationFactory.CreateArenaClientAsync(new StuttererHttpClient(httpClient));
			RoundState[] rounds = await apiClient.GetStatusAsync(CancellationToken.None);
			RoundState round = rounds.First(x => x.CoinjoinState is ConstructionState);

			var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);
			ArenaResponse<Guid> response = await apiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, ownershipProof, CancellationToken.None);

			Assert.NotEqual(Guid.Empty, response.Value);
		}

		private class StuttererHttpClient : HttpClientWrapper
		{
			public StuttererHttpClient(HttpClient httpClient) : base(httpClient)
			{
			}

			public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
			{
				using HttpRequestMessage requestClone1 = request.Clone();
				using HttpRequestMessage requestClone2 = request.Clone();

				HttpResponseMessage result1 = await base.SendAsync(requestClone1, token).ConfigureAwait(false);
				HttpResponseMessage result2 = await base.SendAsync(requestClone2, token).ConfigureAwait(false);

				string content1 = await result1.Content.ReadAsStringAsync(token).ConfigureAwait(false);
				string content2 = await result2.Content.ReadAsStringAsync(token).ConfigureAwait(false);

				Assert.Equal(content1, content2);
				return result2;
			}
		}
	}
}
