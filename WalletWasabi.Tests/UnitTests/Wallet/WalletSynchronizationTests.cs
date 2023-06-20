using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class WalletSynchronizationTests
{
	[Fact]
	// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again.
	// Verifies that the wallet won't find the last TX during Turbo sync but will find it during NonTurbo.
	public async Task InternalAddressReuseNoBlockOverlapTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins.AsAllCoinsView());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(2, coins.AsAllCoinsView().Count());
	}

	[Fact]
	// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again and spend to an external key in a different block.
	// Verifies that the wallet will process the spend correctly when it doesn't have the coins in its CoinsRegistry at the time of spending.
	public async Task InternalAddressReuseThenSpendOnExternalKeyTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node);

		// Self spend the coins to an external key.
		await SendToAsync(wallet, wallet, Money.Coins(2), walletExternalKeyScript, node);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins.Available());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Single(coins.Available());
	}

	[Fact]
	// Reuse 2 internal keys then send all funds away, then receive on first one, send to second one, then send on an external key.
	// This aims to make sure that the CoinsRegistry will catch all the history.
	public async Task InternalAddressReuseChainThenSpendOnExternalKeyTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var secondInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, node);

		// Second address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), secondInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), secondInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, node);

		// Receive again on first internal key
		await SendToAsync(minerWallet, wallet, Money.Coins(3), firstInternalKeyScript, node);

		// Self spend the coins to second internal key
		await SendToAsync(wallet, wallet, Money.Coins(3), secondInternalKeyScript, node);

		// Self spend the coins to an external key
		await SendToAsync(wallet, wallet, Money.Coins(3), walletExternalKeyScript, node);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins.Available());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(7, coins.AsAllCoinsView().Count());
	}

	[Fact]
	// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again but in the same block receive on an external key.
	// Verifies that the wallet will find the TX reusing internal key twice (once in Turbo because of the TX on ext key in the same block and again in NonTurbo), but will process it without issues.
	public async Task InternalAddressReuseWithBlockOverlapTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);

		// Reuse internal key + receive a standard TX in the same block.
		await SendToMempoolAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript);
		await SendToMempoolAsync(minerWallet, wallet, Money.Coins(1), walletExternalKeyScript);
		await node.GenerateBlockAsync(CancellationToken.None);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Equal(3, coins.AsAllCoinsView().Count());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(3, coins.AsAllCoinsView().Count());
	}

	private async Task SendToAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, IDestination destination, MockNode node, CancellationToken cancel = default)
	{
		await SendToMempoolAsync(spendingWallet, receivingWallet, amount, destination, cancel);
		await node.GenerateBlockAsync(cancel);
	}

	private async Task SendToMempoolAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, IDestination destination, CancellationToken cancel = default)
	{
		var tx = await spendingWallet.SendToAsync(amount, destination.ScriptPubKey, FeeRate.Zero, cancel);
		receivingWallet.ScanTransaction(tx);
	}

	private class MockNode
	{
		public Network Network => Network.RegTest;
		public MockRpcClient Rpc { get; }
		public Dictionary<uint256, Block> BlockChain { get; }
		public Dictionary<uint256, Transaction> Mempool { get; }
		public TestWallet Wallet { get; }

		public static async Task<MockNode> CreateNodeAsync()
		{
			var node = new MockNode();
			await node.Wallet.GenerateAsync(101, CancellationToken.None);
			return node;
		}

		private MockNode()
		{
			Rpc = new MockRpcClient();
			BlockChain = new Dictionary<uint256, Block>();
			Mempool = new Dictionary<uint256, Transaction>();
			Wallet = new TestWallet("MinerWallet", Rpc);

			Rpc.OnGenerateToAddressAsync = (blockCount, address) => Task.FromResult(
				Enumerable
					.Range(0, blockCount)
					.Select(_ => CreateBlock(address))
					.Select(block => block.GetHash())
					.ToArray());

			Rpc.OnGetBlockAsync = (blockHash) => Task.FromResult(BlockChain[blockHash]);

			Rpc.OnGetRawTransactionAsync = (txHash, _) => Task.FromResult(
				BlockChain.Values
					.SelectMany(block => block.Transactions)
					.First(tx => tx.GetHash() == txHash));

			Rpc.OnSendRawTransactionAsync = (tx) =>
			{
				var txId = tx.GetHash();
				Mempool.Add(txId, tx);
				return txId;
			};
		}

		public IEnumerable<FilterModel> BuildFilters()
		{
			Dictionary<OutPoint, Script> outPoints = BlockChain.Values
				.SelectMany(block => block.Transactions)
				.SelectMany(tx => tx.Outputs.AsIndexedOutputs())
				.ToDictionary(output => new OutPoint(output.Transaction, output.N), output => output.TxOut.ScriptPubKey);

			List<FilterModel> filters = new();

			var startingFilter = StartingFilters.GetStartingFilter(Network);
			filters.Add(startingFilter);

			foreach (var block in BlockChain.Values)
			{
				var inputScriptPubKeys = block.Transactions
					.SelectMany(tx => tx.Inputs)
					.Where(input => outPoints.ContainsKey(input.PrevOut))
					.Select(input => outPoints[input.PrevOut]);

				var outputScriptPubKeys = block.Transactions
					.SelectMany(tx => tx.Outputs)
					.Select(output => output.ScriptPubKey);

				var scripts = inputScriptPubKeys.Union(outputScriptPubKeys);
				var entries = scripts.Select(x => x.ToCompressedBytes()).DefaultIfEmpty(IndexBuilderService.DummyScript[0]);

				var filter = new GolombRiceFilterBuilder()
					.SetKey(block.GetHash())
					.SetP(20)
					.SetM(1 << 20)
					.AddEntries(entries)
					.Build();

				var tipFilter = filters.Last();

				var smartHeader = new SmartHeader(block.GetHash(), tipFilter.Header.BlockHash, tipFilter.Header.Height + 1, DateTimeOffset.UtcNow);

				filters.Add(new FilterModel(smartHeader, filter));
			}

			return filters;
		}

		private Block CreateBlock(BitcoinAddress address)
		{
			Block block = Network.Consensus.ConsensusFactory.CreateBlock();
			block.Header.HashPrevBlock = BlockChain.Keys.LastOrDefault() ?? uint256.Zero;
			var coinBaseTransaction = Transaction.Create(Network);

			var amount = Money.Coins(5) + Money.Satoshis(BlockChain.Count); // Add block height to make sure the coinbase tx hash differs.
			coinBaseTransaction.Outputs.Add(amount, address);
			block.AddTransaction(coinBaseTransaction);

			foreach (var tx in Mempool.Values)
			{
				block.AddTransaction(tx);
			}
			Mempool.Clear();
			BlockChain.Add(block.GetHash(), block);
			return block;
		}

		public async Task GenerateBlockAsync(CancellationToken cancel) =>
			await Rpc.GenerateToAddressAsync(1, Wallet.GetNextDestination().ScriptPubKey.GetDestinationAddress(Network)!, cancel);
	}

	private class WalletBuilder : IAsyncDisposable
	{
		private IndexStore IndexStore { get; }
		private AllTransactionStore TransactionStore { get; }
		private BitcoinStore BitcoinStore { get; }
		private MemoryCache Cache { get; }
		private HttpClientFactory HttpClientFactory { get; }
		public IEnumerable<FilterModel> Filters { get; }
		public string DataDir { get; }

		public WalletBuilder(MockNode node, [CallerMemberName] string callerName = "NN")
		{
			DataDir = Common.GetWorkDir(nameof(WalletSynchronizationTests), callerName);

			IndexStore = new IndexStore(Path.Combine(DataDir, "indexStore"), node.Network, new SmartHeaderChain());

			TransactionStore = new AllTransactionStore(Path.Combine(DataDir, "transactionStore"), node.Network);

			Filters = node.BuildFilters();

			var blockRepositoryMock = new Mock<IRepository<uint256, Block>>();
			blockRepositoryMock
				.Setup(br => br.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
				.Returns((uint256 hash, CancellationToken _) => Task.FromResult(node.BlockChain[hash])!);
			blockRepositoryMock
				.Setup(br => br.SaveAsync(It.IsAny<Block>(), It.IsAny<CancellationToken>()))
				.Returns((Block _, CancellationToken _) => Task.CompletedTask);

			BitcoinStore = new BitcoinStore(IndexStore, TransactionStore, new MempoolService(), blockRepositoryMock.Object);
			Cache = new MemoryCache(new MemoryCacheOptions());
			HttpClientFactory = new HttpClientFactory(torEndPoint: null, backendUriGetter: () => null!);
		}


		public async Task<WalletWasabi.Wallets.Wallet> CreateRealWalletBasedOnTestWalletAsync(TestWallet wallet)
		{
			await BitcoinStore.InitializeAsync(); //StartingFilter already added to IndexStore after this line.

			await BitcoinStore.IndexStore.AddNewFiltersAsync(Filters.Skip(1));
			var keyManager = KeyManager.CreateNewWatchOnly(wallet.GetSegwitAccountExtPubKey(), null!);
			keyManager.GetKeys(_ => true); //Make sure keys are asserted.

			var serviceConfiguration = new ServiceConfiguration(new UriEndPoint(new Uri("http://www.nomatter.dontcare")), Money.Coins(WalletWasabi.Helpers.Constants.DefaultDustThreshold));
			WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 1000, BitcoinStore, HttpClientFactory);
			HybridFeeProvider feeProvider = new(synchronizer, null);
			SmartBlockProvider blockProvider = new(BitcoinStore.BlockRepository, rpcBlockProvider: null, null, null, Cache);

			return WalletWasabi.Wallets.Wallet.CreateAndRegisterServices(Network.RegTest, BitcoinStore, keyManager, synchronizer, DataDir, serviceConfiguration, feeProvider, blockProvider);
		}

		public async ValueTask DisposeAsync()
		{
			await IndexStore.DisposeAsync();
			await TransactionStore.DisposeAsync();
			await HttpClientFactory.DisposeAsync();
			Cache.Dispose();
		}
	}
}

public static class TestWalletExtensions
{
	public static IDestination GetNextDestination(this TestWallet wallet) =>
		wallet.GetNextDestinations(1, false).Single();

	public static IDestination GetNextInternalDestination(this TestWallet wallet) =>
		wallet.GetNextInternalDestinations(1).Single();
}
