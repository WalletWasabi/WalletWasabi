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
	public async Task InternalAddressReuseNoBlockOverlapTestAsync()
	{
		var baseTestElements = new BaseTestElements();
		var (minerWallet, minerDestination, wallet, destination) = await AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(baseTestElements);

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), destination.ScriptPubKey, baseTestElements, CancellationToken.None);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerDestination.ScriptPubKey, baseTestElements, CancellationToken.None);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), destination.ScriptPubKey, baseTestElements, CancellationToken.None);

		using var wallet1 = await CreateRealWalletBasedOnTestWalletAsync(baseTestElements, wallet, destination, "InternalAddressReuseNoBlockOverlapTestAsync");

		await wallet1.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		await wallet1.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);

		var coins = wallet1.Coins as CoinsRegistry;
		Assert.Equal(2, coins!.AsAllCoinsView().Count());
	}

	[Fact]
	public async Task InternalAddressReuseWithBlockOverlapTestAsync()
	{
		var baseTestElements = new BaseTestElements();
		var (minerWallet, minerDestination, wallet, destination) = await AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(baseTestElements);
		var walletExtDestination = wallet.GetNextDestinations(1, false).Single();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), destination.ScriptPubKey, baseTestElements, CancellationToken.None);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerDestination.ScriptPubKey, baseTestElements, CancellationToken.None);

		// Reuse internal key + receive a standard TX in the same block.
		var reuseInternalKeyTx = new TxSkeleton(Money.Coins(2), destination.ScriptPubKey, baseTestElements.FeeRate, minerWallet);
		var receiveStandardTx = new TxSkeleton(Money.Coins(3), walletExtDestination.ScriptPubKey, baseTestElements.FeeRate, minerWallet);
		SendSeveralTxSameBlock(new[] { reuseInternalKeyTx, receiveStandardTx }, baseTestElements, minerDestination.ScriptPubKey, CancellationToken.None);

		using var wallet1 = await CreateRealWalletBasedOnTestWalletAsync(baseTestElements, wallet, destination, "InternalAddressReuseWithBlockOverlapTestAsync");

		await wallet1.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		await wallet1.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);

		var coins = wallet1.Coins as CoinsRegistry;
		Assert.Equal(3, coins!.AsAllCoinsView().Count());
	}

	private async Task<WalletWasabi.Wallets.Wallet> CreateRealWalletBasedOnTestWalletAsync(BaseTestElements baseTestElements, TestWallet wallet, IDestination oneKeyOfTestWallet, string callerName)
	{
		KeyManager keyManager = KeyManager.CreateNewWatchOnly(wallet.ExtKey.Derive(KeyPath.Parse("m/84'/0'/0'")).Neuter(), null!);
		var keys = keyManager.GetKeys(k => true); //Make sure keys are asserted.

		Assert.Contains(keys.Where(key => key.IsInternal), key => key.P2wpkhScript == oneKeyOfTestWallet.ScriptPubKey);

		var dir = Common.GetWorkDir("WalletSynchronizationTests", callerName);

		// Warning disabled because objects are disposed in the calling function.
#pragma warning disable CA2000 // Dispose objects before losing scope
		var indexStore = new IndexStore(Path.Combine(dir, "indexStore"), baseTestElements.Network, new SmartHeaderChain());

		var transactionStore = new AllTransactionStore(Path.Combine(dir, "transactionStore"), baseTestElements.Network);
#pragma warning restore CA2000 // Dispose objects before losing scope

		var mempoolService = new MempoolService();

		var blockRepositoryMock = new Mock<IRepository<uint256, Block>>();
		blockRepositoryMock
			.Setup(br => br.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.Returns((uint256 hash, CancellationToken _) => Task.FromResult(baseTestElements.BlockChain[hash])!);
		blockRepositoryMock
			.Setup(br => br.SaveAsync(It.IsAny<Block>(), It.IsAny<CancellationToken>()))
			.Returns((Block _, CancellationToken _) => Task.CompletedTask);

		var bitcoinStore = new BitcoinStore(indexStore, transactionStore, mempoolService, blockRepositoryMock.Object);
		await bitcoinStore.InitializeAsync(); //StartingFilter already added to IndexStore after this line.

		var filters = BuildFiltersForBlockChain(baseTestElements);
		await indexStore.AddNewFiltersAsync(filters.Skip(1));

		var serviceConfiguration = new ServiceConfiguration(new UriEndPoint(new Uri("http://www.nomatter.dontcare")), Money.Coins(WalletWasabi.Helpers.Constants.DefaultDustThreshold));
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => null!);
		WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 1000, bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);
		using MemoryCache cache = new(new MemoryCacheOptions());
		await using SpecificNodeBlockProvider specificNodeBlockProvider = new(baseTestElements.Network, serviceConfiguration, null);
		SmartBlockProvider blockProvider = new(bitcoinStore.BlockRepository, rpcBlockProvider: null, null, null, cache);

		return WalletWasabi.Wallets.Wallet.CreateAndRegisterServices(baseTestElements.Network, bitcoinStore, keyManager, synchronizer, dir, serviceConfiguration, feeProvider, blockProvider);
	}
	private async Task<(TestWallet, IDestination, TestWallet, IDestination)> AddBaseRpcFunctionalitiesAndCreateTestWalletsAsync(BaseTestElements baseTestElements)
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
		var minerDestination = minerWallet.GetNextDestinations(1, false).Single();

		var wallet = new TestWallet("wallet", baseTestElements.Rpc);
		var destination = wallet.GetNextInternalDestinations(1).Single();

		baseTestElements.Rpc.OnSendRawTransactionAsync = (tx) =>
		{
			CreateBlock(baseTestElements, minerDestination.ScriptPubKey.GetDestinationAddress(baseTestElements.Network)!, new[] { tx });
			return tx.GetHash();
		};

		return (minerWallet, minerDestination, wallet, destination);
	}

	private async Task SendToAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, Script destinationScript, BaseTestElements baseTestElements, CancellationToken cancel)
	{
		var tx = await spendingWallet.SendToAsync(amount, destinationScript, baseTestElements.FeeRate, cancel);
		receivingWallet.ScanTransaction(tx);
	}

	private Block CreateBlock(BaseTestElements baseTestElements, BitcoinAddress address, IEnumerable<Transaction>? transactions = null)
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

	private IEnumerable<FilterModel> BuildFiltersForBlockChain(BaseTestElements baseTestElements)
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

	private IEnumerable<uint256> SendSeveralTxSameBlock(IEnumerable<TxSkeleton> txs, BaseTestElements baseTestElements, Script minerDestination, CancellationToken cancel)
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

	private class BaseTestElements
	{
		public Network Network { get; }
		public FeeRate FeeRate { get; }
		public MockRpcClient Rpc { get; }
		public Dictionary<uint256, Block> BlockChain { get; }

		public BaseTestElements()
		{
			Network = Network.RegTest;
			FeeRate = FeeRate.Zero;
			Rpc = new MockRpcClient();
			BlockChain = new Dictionary<uint256, Block>();
		}
	}

	private record TxSkeleton(Money Amount, Script ScriptPubKey, FeeRate FeeRate, TestWallet SpendingWallet);
}
