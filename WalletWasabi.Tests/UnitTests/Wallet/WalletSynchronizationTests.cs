using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using WalletWasabi.Extensions;
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
		await using var testSetup = new TestSetup("InternalAddressReuseNoBlockOverlapTestAsync");
		var (minerWallet, wallet) = await AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(testSetup);

		var minerFirstKeyScript = minerWallet.GetNextDestinations(1, false).Single().ScriptPubKey;
		var firstInternalKeyScript = wallet.GetNextInternalDestinations(1).Single().ScriptPubKey;

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, testSetup, CancellationToken.None);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, testSetup, CancellationToken.None);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, testSetup, CancellationToken.None);

		using var wallet1 = await CreateRealWalletBasedOnTestWalletAsync(testSetup, wallet, firstInternalKeyScript, "InternalAddressReuseNoBlockOverlapTestAsync");
		var coins = wallet1.Coins as CoinsRegistry;

		await wallet1.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins!.AsAllCoinsView());

		await wallet1.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(2, coins!.AsAllCoinsView().Count());
	}

	[Fact]
	// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again and spend to an external key in a different block.
	// Verifies that the wallet will process the spend correctly when it doesn't have the coins in its CoinsRegistry at the time of spending.
	public async Task InternalAddressReuseThenSpendOnExternalKeyTestAsync()
	{
		await using var testSetup = new TestSetup("InternalAddressReuseThenSpendOnExternalKeyTestAsync");
		var (minerWallet, wallet) = await AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(testSetup);

		var minerFirstKeyScript = minerWallet.GetNextDestinations(1, false).Single().ScriptPubKey;
		var firstInternalKeyScript = wallet.GetNextInternalDestinations(1).Single().ScriptPubKey;
		var walletExternalKeyScript = wallet.GetNextDestinations(1, false).Single().ScriptPubKey;

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, testSetup, CancellationToken.None);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, testSetup, CancellationToken.None);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, testSetup, CancellationToken.None);

		// Self spend the coins to an external key.
		await SendToAsync(wallet, wallet, Money.Coins(2), walletExternalKeyScript, testSetup, CancellationToken.None);

		using var wallet1 = await CreateRealWalletBasedOnTestWalletAsync(testSetup, wallet, firstInternalKeyScript, "InternalAddressReuseNoBlockOverlapTestAsync");
		var coins = wallet1.Coins as CoinsRegistry;

		await wallet1.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins!.Available());

		await wallet1.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Single(coins!.Available());
	}

	[Fact]
	// Reuse 2 internal keys then send all funds away, then receive on first one, send to second one, then send on an external key.
	// This aims to make sure that the CoinsRegistry will catch all the history.
	public async Task InternalAddressReuseChainThenSpendOnExternalKeyTestAsync()
	{
		await using var testSetup = new TestSetup("InternalAddressReuseThenSpendOnExternalKeyTestAsync");
		var (minerWallet, wallet) = await AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(testSetup);

		var minerFirstKeyScript = minerWallet.GetNextDestinations(1, false).Single().ScriptPubKey;
		var firstInternalKeyScript = wallet.GetNextInternalDestinations(1).Single().ScriptPubKey;
		var secondInternalKeyScript = wallet.GetNextInternalDestinations(1).Single().ScriptPubKey;
		var walletExternalKeyScript = wallet.GetNextDestinations(1, false).Single().ScriptPubKey;

		// First address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, testSetup, CancellationToken.None);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, testSetup, CancellationToken.None);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, testSetup, CancellationToken.None);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, testSetup, CancellationToken.None);

		// Second address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), secondInternalKeyScript, testSetup, CancellationToken.None);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, testSetup, CancellationToken.None);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), secondInternalKeyScript, testSetup, CancellationToken.None);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, testSetup, CancellationToken.None);

		// Receive again on first internal key
		await SendToAsync(minerWallet, wallet, Money.Coins(3), firstInternalKeyScript, testSetup, CancellationToken.None);

		// Self spend the coins to second internal key
		await SendToAsync(wallet, wallet, Money.Coins(3), secondInternalKeyScript, testSetup, CancellationToken.None);

		// Self spend the coins to an external key
		await SendToAsync(wallet, wallet, Money.Coins(3), walletExternalKeyScript, testSetup, CancellationToken.None);

		using var wallet1 = await CreateRealWalletBasedOnTestWalletAsync(testSetup, wallet, firstInternalKeyScript, "InternalAddressReuseNoBlockOverlapTestAsync");
		var coins = wallet1.Coins as CoinsRegistry;

		await wallet1.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins!.Available());

		await wallet1.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(7, coins!.AsAllCoinsView().Count());
	}

	[Fact]
	// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again but in the same block receive on an external key.
	// Verifies that the wallet will find the TX reusing internal key twice (once in Turbo because of the TX on ext key in the same block and again in NonTurbo), but will process it without issues.
	public async Task InternalAddressReuseWithBlockOverlapTestAsync()
	{
		await using var testSetup = new TestSetup("InternalAddressReuseWithBlockOverlapTestAsync");
		var (minerWallet, wallet) = await AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(testSetup);

		var minerFirstKeyScript = minerWallet.GetNextDestinations(1, false).Single().ScriptPubKey;
		var firstInternalKeyScript = wallet.GetNextInternalDestinations(1).Single().ScriptPubKey;
		var walletExternalKeyScript = wallet.GetNextDestinations(1, false).Single().ScriptPubKey;

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, testSetup, CancellationToken.None);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, testSetup, CancellationToken.None);

		// Reuse internal key + receive a standard TX in the same block.
		var reuseInternalKeyTx = new TxSkeleton(Money.Coins(2), firstInternalKeyScript, testSetup.FeeRate, minerWallet);
		var receiveStandardTx = new TxSkeleton(Money.Coins(3), walletExternalKeyScript, testSetup.FeeRate, minerWallet);
		SendSeveralTxSameBlock(new[] { reuseInternalKeyTx, receiveStandardTx }, testSetup, minerFirstKeyScript, CancellationToken.None);

		using var wallet1 = await CreateRealWalletBasedOnTestWalletAsync(testSetup, wallet, firstInternalKeyScript, "InternalAddressReuseWithBlockOverlapTestAsync");
		var coins = wallet1.Coins as CoinsRegistry;

		await wallet1.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Equal(3, coins!.AsAllCoinsView().Count());

		await wallet1.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(3, coins!.AsAllCoinsView().Count());
	}

	private async Task<WalletWasabi.Wallets.Wallet> CreateRealWalletBasedOnTestWalletAsync(TestSetup testSetup, TestWallet wallet, Script oneScriptPubKeyOfTestWallet, string callerName)
	{
		KeyManager keyManager = KeyManager.CreateNewWatchOnly(wallet.ExtKey.Derive(KeyPath.Parse("m/84'/0'/0'")).Neuter(), null!);
		var keys = keyManager.GetKeys(k => true); //Make sure keys are asserted.

		Assert.Contains(keys.Where(key => key.IsInternal), key => key.P2wpkhScript == oneScriptPubKeyOfTestWallet);

		var indexStore = testSetup.IndexStore;

		var transactionStore = testSetup.TransactionStore;

		var mempoolService = new MempoolService();

		var blockRepositoryMock = new Mock<IRepository<uint256, Block>>();
		blockRepositoryMock
			.Setup(br => br.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.Returns((uint256 hash, CancellationToken _) => Task.FromResult(testSetup.BlockChain[hash])!);
		blockRepositoryMock
			.Setup(br => br.SaveAsync(It.IsAny<Block>(), It.IsAny<CancellationToken>()))
			.Returns((Block _, CancellationToken _) => Task.CompletedTask);

		var bitcoinStore = new BitcoinStore(indexStore, transactionStore, mempoolService, blockRepositoryMock.Object);
		await bitcoinStore.InitializeAsync(); //StartingFilter already added to IndexStore after this line.

		var filters = BuildFiltersForBlockChain(testSetup);
		await indexStore.AddNewFiltersAsync(filters.Skip(1));

		var serviceConfiguration = new ServiceConfiguration(new UriEndPoint(new Uri("http://www.nomatter.dontcare")), Money.Coins(WalletWasabi.Helpers.Constants.DefaultDustThreshold));
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => null!);
		WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 1000, bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);
		using MemoryCache cache = new(new MemoryCacheOptions());
		await using SpecificNodeBlockProvider specificNodeBlockProvider = new(testSetup.Network, serviceConfiguration, null);
		SmartBlockProvider blockProvider = new(bitcoinStore.BlockRepository, rpcBlockProvider: null, null, null, cache);

		return WalletWasabi.Wallets.Wallet.CreateAndRegisterServices(testSetup.Network, bitcoinStore, keyManager, synchronizer, testSetup.Dir, serviceConfiguration, feeProvider, blockProvider);
	}
	private async Task<(TestWallet, TestWallet)> AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(TestSetup baseTestElements)
	{
		baseTestElements.Rpc.OnGenerateToAddressAsync = (blockCount, address) => Task.FromResult(
			Enumerable
				.Range(0, blockCount)
				.Select(_ => CreateBlock(baseTestElements, address))
				.Select(block => block.GetHash())
				.ToArray());

		baseTestElements.Rpc.OnGetBlockAsync = (blockHash) => Task.FromResult(baseTestElements.BlockChain[blockHash]);

		baseTestElements.Rpc.OnGetRawTransactionAsync = (txHash, _) => Task.FromResult(
			baseTestElements.BlockChain.Values
				.SelectMany(block => block.Transactions)
				.First(tx => tx.GetHash() == txHash));


		var minerWallet = new TestWallet("MinerWallet", baseTestElements.Rpc);
		await minerWallet.GenerateAsync(101, CancellationToken.None);
		var minerCoinbaseDestinationScript = minerWallet.GetNextDestinations(1, false).Single().ScriptPubKey;

		var wallet = new TestWallet("wallet", baseTestElements.Rpc);

		baseTestElements.Rpc.OnSendRawTransactionAsync = (tx) =>
		{
			CreateBlock(baseTestElements, minerCoinbaseDestinationScript.GetDestinationAddress(baseTestElements.Network)!, new[] { tx });
			return tx.GetHash();
		};

		return (minerWallet, wallet);
	}

	private async Task SendToAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, Script destinationScript, TestSetup testSetup, CancellationToken cancel)
	{
		var tx = await spendingWallet.SendToAsync(amount, destinationScript, testSetup.FeeRate, cancel);
		receivingWallet.ScanTransaction(tx);
	}

	private Block CreateBlock(TestSetup baseTestElements, BitcoinAddress address, IEnumerable<Transaction>? transactions = null)
	{
		Block block = baseTestElements.Network.Consensus.ConsensusFactory.CreateBlock();
		block.Header.HashPrevBlock = baseTestElements.BlockChain.Keys.LastOrDefault() ?? uint256.Zero;
		var coinBaseTransaction = Transaction.Create(baseTestElements.Network);

		var amount = Money.Coins(5) + Money.Satoshis(baseTestElements.BlockChain.Count); // Add block height to make sure the coinbase tx hash differs.
		coinBaseTransaction.Outputs.Add(amount, address);
		block.AddTransaction(coinBaseTransaction);

		if (transactions is not null)
		{
			foreach (var tx in transactions)
			{
				block.AddTransaction(tx);
			}
		}

		baseTestElements.BlockChain.Add(block.GetHash(), block);
		return block;
	}

	private IEnumerable<FilterModel> BuildFiltersForBlockChain(TestSetup baseTestElements)
	{
		Dictionary<OutPoint, Script> outPoints = baseTestElements.BlockChain.Values
			.SelectMany(block => block.Transactions)
			.SelectMany(tx => tx.Outputs.AsIndexedOutputs())
			.ToDictionary(output => new OutPoint(output.Transaction, output.N), output => output.TxOut.ScriptPubKey);

		List<FilterModel> filters = new();

		var startingFilter = StartingFilters.GetStartingFilter(baseTestElements.Network);
		filters.Add(startingFilter);

		foreach (var block in baseTestElements.BlockChain.Values)
		{
			var inputScriptPubKeys = block.Transactions
				.SelectMany(tx => tx.Inputs)
				.Where(input => outPoints.ContainsKey(input.PrevOut))
				.Select(input => outPoints[input.PrevOut]);

			var outputScriptPubKeys = block.Transactions
				.SelectMany(tx => tx.Outputs)
				.Select(output => output.ScriptPubKey);

			var scripts = inputScriptPubKeys.Union(outputScriptPubKeys);
			var entries = scripts.Any() ? scripts.Select(x => x.ToCompressedBytes()) : IndexBuilderService.DummyScript;

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

	private IEnumerable<uint256> SendSeveralTxSameBlock(IEnumerable<TxSkeleton> txs, TestSetup baseTestElements, Script minerDestination, CancellationToken cancel)
	{
		List<(Transaction Tx, TestWallet SpendingWallet)> signedTxsWithSigner = new();
		const int FinalSignedTxVirtualSize = 222;

		foreach (var txSkeleton in txs)
		{
			var effectiveOutputCost = txSkeleton.Amount + txSkeleton.FeeRate.GetFeeWithZero(FinalSignedTxVirtualSize);
			var tx = txSkeleton.SpendingWallet.CreateSelfTransfer(FeeRate.Zero);

			if (tx.Outputs[0].Value < effectiveOutputCost)
			{
				throw new ArgumentException("Not enough satoshis in input.");
			}

			if (effectiveOutputCost != tx.Outputs[0].Value)
			{
				tx.Outputs[0].Value -= effectiveOutputCost;
				tx.Outputs.Add(txSkeleton.Amount, txSkeleton.ScriptPubKey);
			}
			else
			{
				// Sending whole coin.
				tx.Outputs[0].ScriptPubKey = txSkeleton.ScriptPubKey;
			}
			signedTxsWithSigner.Add((txSkeleton.SpendingWallet.SignTransaction(tx), txSkeleton.SpendingWallet));
			txSkeleton.SpendingWallet.ScanTransaction(tx);
		}

		CreateBlock(baseTestElements, minerDestination.GetDestinationAddress(baseTestElements.Network)!, signedTxsWithSigner.Select(x => x.Tx));

		return signedTxsWithSigner.Select(x => x.Tx.GetHash());
	}

	private class TestSetup : IAsyncDisposable
	{
		public Network Network { get; }
		public FeeRate FeeRate { get; }
		public MockRpcClient Rpc { get; }
		public Dictionary<uint256, Block> BlockChain { get; }
		public IndexStore IndexStore { get; }
		public AllTransactionStore TransactionStore { get; }
		public string Dir { get; }

		public TestSetup(string callerName)
		{
			Network = Network.RegTest;
			FeeRate = FeeRate.Zero;
			Rpc = new MockRpcClient();
			BlockChain = new Dictionary<uint256, Block>();
			Dir = Common.GetWorkDir("WalletSynchronizationTests", callerName);
			IndexStore = new IndexStore(Path.Combine(Dir, "indexStore"), Network, new SmartHeaderChain());
			TransactionStore = new AllTransactionStore(Path.Combine(Dir, "transactionStore"), Network);
		}

		public async ValueTask DisposeAsync()
		{
			await IndexStore.DisposeAsync();
			await TransactionStore.DisposeAsync();
		}
	}

	private record TxSkeleton(Money Amount, Script ScriptPubKey, FeeRate FeeRate, TestWallet SpendingWallet);
}
