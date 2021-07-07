using System;
using System.Collections.Generic;
using System.IO;
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
			   await apiClient.RegisterInputAsync(round.Id, nonExistingOutPoint, signingKey, CancellationToken.None));

			var wex = Assert.IsType<WabiSabiProtocolException>(ex.InnerException);
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, wex.ErrorCode);
		}

		[Theory]
		[InlineData(new long[] { 20_000_000, 40_000_000, 60_000_000, 80_000_000 })]
		[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 })]
		[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 }, new long[] { 100_000_000, 100_000_000 })]
		[InlineData(new long[] { 120_000_000 }, new long[] { 20_000_000, 40_000_000, 60_000_000 })]
		[InlineData(new long[] { 100_000_000, 10_000_000, 10_000 })]
		public async Task SoloCoinJoinTestAsync(long[] amounts, long[]? outputs = null)
		{
			int inputCount = amounts.Length;

			// At the end of the test a coinjoin transaction has to be created and broadcasted.
			var transactionCompleted = new TaskCompletionSource<Transaction>();

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
			cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

			// Create a key manager and use it to create two fake coins.
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
			await coinJoinClient.StartCoinJoinAsync(cts.Token, outputs?.Select(s => Money.Satoshis(s)));

			var boadcastedTx = await transactionCompleted.Task.ConfigureAwait(false); // wait for the transaction to be broadcasted.
			Assert.NotNull(boadcastedTx);

			await roundStateUpdater.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task MultiClientsCoinJoinTestAsync()
		{
			int inputCount = 10;

			// At the end of the test a coinjoin transaction has to be created and broadcasted.
			var transactionCompleted = new TaskCompletionSource<Transaction>();

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

			var rpc = GetStatefullMockRpc(transactionCompleted);

			var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					// Instruct the coodinator DI container to use these two scoped
					// services to build everything (wabisabi controller, arena, etc)
					services.AddScoped<IRPCClient>(s => rpc);
					services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig { MaxInputCountByRound = inputCount });
				});
			}).CreateClient();

			// Create the coinjoin client
			var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);

			var nuberOfParticipants = 10;

			var participants = Enumerable
				.Range(0, nuberOfParticipants)
				.Select(_ => new Participant(rpc, apiClient))
				.ToArray();

			foreach (var participant in participants)
			{
				await participant.InitializeAsync(2, cts.Token);
			}
			foreach (var participant in participants)
			{
				_ = Task.Run(async () => await participant.StartParticipatingAsync(cts.Token));
			}

			var boadcastedTx = await transactionCompleted.Task;
			Assert.NotNull(boadcastedTx);
		}

		private IRPCClient GetStatefullMockRpc(TaskCompletionSource<Transaction> transactionCompleted)
		{
			var rpc = BitcoinFactory.GetMockMinimalRpc();

			var blocks = new List<Block>();
			var mempool = new List<Transaction>();

			// Declarations
			var confirmedTransactions = blocks.SelectMany(b => b.Transactions);
			var transactions = confirmedTransactions.Concat(mempool);
			var coins = transactions.SelectMany(t => t.Outputs.Select(o => new Coin(t, o)));
			var spentCoins = transactions.SelectMany(t => t.Inputs.Where(i => i.PrevOut.Hash != uint256.Zero).Select(i => coins.Single(c => c.Outpoint == i.PrevOut)));
			var unspentCoins = coins.Except(spentCoins);

			// Make the coordinator to believe that those two coins are real and
			// that they exist in the blockchain with many confirmations.
			rpc.OnGetTxOutAsync = (txId, idx, _) =>
			{
				var coin = unspentCoins.FirstOrDefault(c => (c.Outpoint.Hash, c.Outpoint.N) == (txId, idx));
				if (coin is null)
				{
					return null;
				}
				return new()
				{
					Confirmations = 101,
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					TxOut = coin.TxOut
				};
			};

			rpc.OnGetBlockAsync = (blockId) =>
				Task.FromResult(blocks.First(b => b.GetHash() == blockId));

			// Make the coordinator believe that the transaction is being
			// broadcasted using the RPC interface. Once we receive this tx
			// (the `SendRawTransationAsync` was invoked) we stop waiting
			// and finish the waiting tasks to finish the test successfully.
			rpc.OnSendRawTransactionAsync = (tx) =>
			{
				mempool.Add(tx);
				transactionCompleted.TrySetResult(tx);
				return tx.GetHash();
			};

			rpc.OnGenerateToAddressAsync = (n, address) =>
			{
				var block = Block
					.CreateBlock(Network.Main)
					.CreateNextBlockWithCoinbase(address, blocks.Count);

				blocks.Add(block);
				return Task.FromResult(new[] { block.GetHash() });
			};

			return rpc;
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
	}
}
