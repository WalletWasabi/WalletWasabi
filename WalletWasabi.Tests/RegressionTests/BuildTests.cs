using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

[Collection("RegTest collection")]
public class BuildTests
{
	public BuildTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task BuildTransactionValidationsTestAsync()
	{
		(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Helpers.Common.GetWorkDir();

		using MemoryCache cache = CreateMemoryCache();

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			rpcBlockProvider: null,
			specificNodeBlockProvider: new SpecificNodeBlockProvider(network, serviceConfiguration, httpClientFactory: httpClientFactory),
			p2PBlockProvider: new P2PBlockProvider(network, nodes, httpClientFactory),
			cache);

		using var wallet = Wallet.CreateAndRegisterServices(network, bitcoinStore, keyManager, synchronizer, workDir, serviceConfiguration, feeProvider, blockProvider);
		wallet.NewFilterProcessed += Common.Wallet_NewFilterProcessed;

		var scp = new Key().PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main);

		PaymentIntent validIntent = new(scp, Money.Coins(1));
		PaymentIntent invalidIntent = new(new DestinationRequest(scp, Money.Coins(10 * 1000 * 1000)), new DestinationRequest(scp, Money.Coins(12 * 1000 * 1000)));

		Assert.Throws<OverflowException>(() => new PaymentIntent(
			new DestinationRequest(scp, Money.Satoshis(long.MaxValue)),
			new DestinationRequest(scp, Money.Satoshis(long.MaxValue)),
			new DestinationRequest(scp, Money.Satoshis(5))));

		Logger.TurnOff();

		// toSend cannot have a zero element
		Assert.Throws<ArgumentException>(() => wallet.BuildTransaction("", new PaymentIntent(Array.Empty<DestinationRequest>()), FeeStrategy.SevenDaysConfirmationTargetStrategy));

		// feeTarget has to be in the range 0 to 1008
		Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction("", validIntent, FeeStrategy.CreateFromConfirmationTarget(-10)));
		Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction("", validIntent, FeeStrategy.CreateFromConfirmationTarget(2000)));

		// toSend amount sum has to be in range 0 to 2099999997690000
		Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction("", invalidIntent, FeeStrategy.TwentyMinutesConfirmationTargetStrategy));

		// toSend negative sum amount
		Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction("", new PaymentIntent(scp, Money.Satoshis(-10000)), FeeStrategy.TwentyMinutesConfirmationTargetStrategy));

		// toSend negative operation amount
		Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction(
			"",
			new PaymentIntent(
				new DestinationRequest(scp, Money.Satoshis(20000)),
				new DestinationRequest(scp, Money.Satoshis(-10000))),
			FeeStrategy.TwentyMinutesConfirmationTargetStrategy));

		// allowedInputs cannot be empty
		Assert.Throws<ArgumentException>(() => wallet.BuildTransaction("", validIntent, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowedInputs: Array.Empty<OutPoint>()));

		// "Only one element can contain the AllRemaining flag.
		Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(
			password,
			new PaymentIntent(
				new DestinationRequest(scp, MoneyRequest.CreateAllRemaining(), "zero"),
				new DestinationRequest(scp, MoneyRequest.CreateAllRemaining(), "zero")),
			FeeStrategy.SevenDaysConfirmationTargetStrategy,
			false));

		// Get some money, make it confirm.
		var txId = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network), Money.Coins(1m));

		// Generate some coins
		await rpc.GenerateAsync(2);

		try
		{
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.

			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
			{
				await wallet.StartAsync(cts.Token); // Initialize wallet service.
			}

			// subtract Fee from amount index with no enough money
			PaymentIntent operations = new(new DestinationRequest(scp, Money.Coins(1m), subtractFee: true), new DestinationRequest(scp, Money.Coins(0.5m)));
			Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, false));

			// No enough money (only one confirmed coin, no unconfirmed allowed)
			operations = new PaymentIntent(scp, Money.Coins(1.5m));
			Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction("", operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy));

			// No enough money (only one confirmed coin, unconfirmed allowed)
			Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction("", operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, true));

			// Add new money with no confirmation
			var txId2 = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("bar").GetP2wpkhAddress(network), Money.Coins(2m));
			await Task.Delay(1000); // Wait tx to arrive and get processed.

			// Enough money (one confirmed coin and one unconfirmed coin, unconfirmed are NOT allowed)
			Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction("", operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, false));

			// Enough money (one unconfirmed coin, unconfirmed are allowed)
			var btx = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, true);
			var spentCoin = Assert.Single(btx.SpentCoins);
			Assert.False(spentCoin.Confirmed);

			// Enough money (one confirmed coin and one unconfirmed coin, unconfirmed are allowed)
			operations = new PaymentIntent(scp, Money.Coins(2.5m));
			btx = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, true);
			Assert.Equal(2, btx.SpentCoins.Count());
			Assert.Equal(1, btx.SpentCoins.Count(c => c.Confirmed));
			Assert.Equal(1, btx.SpentCoins.Count(c => !c.Confirmed));

			// Only one operation with AllRemainingFlag

			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(
				"",
				new PaymentIntent(
					new DestinationRequest(scp, MoneyRequest.CreateAllRemaining()),
					new DestinationRequest(scp, MoneyRequest.CreateAllRemaining())),
				FeeStrategy.TwentyMinutesConfirmationTargetStrategy));

			Logger.TurnOn();

			operations = new PaymentIntent(scp, Money.Coins(0.5m));
			btx = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy);
		}
		finally
		{
			await wallet.StopAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	[Fact]
	public async Task BuildTransactionReorgsTestAsync()
	{
		(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);
		bitcoinStore.IndexStore.NewFilter += Common.Wallet_NewFilterProcessed;
		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.
		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Helpers.Common.GetWorkDir();
		using MemoryCache cache = CreateMemoryCache();

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			null,
			new SpecificNodeBlockProvider(network, serviceConfiguration, httpClientFactory: httpClientFactory),
			new P2PBlockProvider(network, nodes, httpClientFactory),
			cache);

		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir));
		walletManager.RegisterServices(bitcoinStore, synchronizer, serviceConfiguration, feeProvider, blockProvider);

		var baseTip = await rpc.GetBestBlockHashAsync();

		// Generate script
		using var k = new Key();
		var scp = k.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo label");
		var fundingTxId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));

		// Generate some coins
		await rpc.GenerateAsync(2);

		try
		{
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);
			using var wallet = await walletManager.AddAndStartWalletAsync(keyManager);
			var coin = Assert.Single(wallet.Coins);
			Assert.True(coin.Confirmed);
			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			// Send money before reorg.
			PaymentIntent operations = new(scp, Money.Coins(0.011m));
			var btx1 = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy);
			await broadcaster.SendTransactionAsync(btx1.Transaction);
			var coin2 = Assert.Single(wallet.Coins);
			Assert.NotEqual(coin, coin2);
			Assert.False(coin2.Confirmed);

			operations = new PaymentIntent(scp, Money.Coins(0.012m));
			var btx2 = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);
			await broadcaster.SendTransactionAsync(btx2.Transaction);
			var coin3 = Assert.Single(wallet.Coins);
			Assert.NotEqual(coin2, coin3);
			Assert.False(coin3.Confirmed);

			// Test synchronization after fork.
			// Invalidate the blocks containing the funding transaction
			var tip = await rpc.GetBestBlockHashAsync();
			await rpc.InvalidateBlockAsync(tip); // Reorg 1
			tip = await rpc.GetBestBlockHashAsync();
			await rpc.InvalidateBlockAsync(tip); // Reorg 2

			// Generate three new blocks (replace the previous invalidated ones)
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(3);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 3);
			await Task.Delay(100); // Wait for tx processing.

			var coin4 = Assert.Single(wallet.Coins);
			Assert.Equal(coin3, coin4);
			Assert.True(coin.Confirmed);
			Assert.True(coin2.Confirmed);
			Assert.True(coin3.Confirmed);
			Assert.True(coin4.Confirmed);

			// Send money after reorg.
			// When we invalidate a block, the transactions set in the invalidated block
			// are reintroduced when we generate a new block through the rpc call
			operations = new PaymentIntent(scp, Money.Coins(0.013m));
			var btx3 = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy);
			await broadcaster.SendTransactionAsync(btx3.Transaction);
			var coin5 = Assert.Single(wallet.Coins);
			Assert.NotEqual(coin4, coin5);
			Assert.False(coin5.Confirmed);

			operations = new PaymentIntent(scp, Money.Coins(0.014m));
			var btx4 = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);
			await broadcaster.SendTransactionAsync(btx4.Transaction);
			var coin6 = Assert.Single(wallet.Coins);
			Assert.NotEqual(coin5, coin6);
			Assert.False(coin6.Confirmed);

			// Test synchronization after fork with different transactions.
			// Create a fork that invalidates the blocks containing the funding transaction
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.InvalidateBlockAsync(baseTip);
			try
			{
				await rpc.AbandonTransactionAsync(fundingTxId);
			}
			catch
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					throw;
				}
				return; // Occasionally this fails on Linux or OSX, I have no idea why.
			}
			// Spend the inputs of the tx so we know
			var success = bitcoinStore.TransactionStore.TryGetTransaction(fundingTxId, out var invalidSmartTransaction);
			Assert.True(success);
			var invalidCoin = Assert.Single(((CoinsRegistry)wallet.Coins).AsAllCoinsView().CreatedBy(invalidSmartTransaction!.GetHash()));
			Assert.NotNull(invalidCoin.SpenderTransaction);
			Assert.True(invalidCoin.Confirmed);

			var overwriteTx = Transaction.Create(network);
			overwriteTx.Inputs.AddRange(invalidSmartTransaction.Transaction.Inputs);
			var walletAddress = keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network);
			bool onAddress = false;
			foreach (var invalidOutput in invalidSmartTransaction.Transaction.Outputs)
			{
				if (onAddress)
				{
					overwriteTx.Outputs.Add(new TxOut(invalidOutput.Value, new Key().GetAddress(ScriptPubKeyType.Segwit, network)));
				}
				else
				{
					overwriteTx.Outputs.Add(new TxOut(invalidOutput.Value, walletAddress));
					onAddress = true;
				}
			}
			var srtxwwreq = new SignRawTransactionRequest();
			srtxwwreq.Transaction = overwriteTx;
			var srtxwwres = await rpc.SignRawTransactionWithWalletAsync(srtxwwreq);

			var eventAwaiter = new EventAwaiter<ProcessedResult>(
				h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed += h,
				h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed -= h);
			await rpc.SendRawTransactionAsync(srtxwwres.SignedTransaction);
			await rpc.GenerateAsync(10);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 10);
			var eventArgs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
			var doubleSpend = Assert.Single(eventArgs.SuccessfullyDoubleSpentCoins);
			Assert.Equal(invalidCoin.TransactionId, doubleSpend.TransactionId);

			var curBlockHash = await rpc.GetBestBlockHashAsync();
			blockCount = await rpc.GetBlockCountAsync();
			Assert.Equal(bitcoinStore.SmartHeaderChain.TipHash, curBlockHash);
			Assert.Equal((int)bitcoinStore.SmartHeaderChain.TipHeight, blockCount);

			// Make sure the funding transaction is not in any block of the chain
			while (curBlockHash != rpc.Network.GenesisHash)
			{
				var block = await rpc.GetBlockAsync(curBlockHash);

				if (block.Transactions.Any(tx => tx.GetHash() == fundingTxId))
				{
					throw new Exception($"Transaction found in block at height {blockCount} hash: {block.GetHash()}");
				}
				curBlockHash = block.Header.HashPrevBlock;
				blockCount--;
			}

			// Get some money, make it confirm.
			// this is necessary because we are in a fork now.
			eventAwaiter = new EventAwaiter<ProcessedResult>(
							h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed += h,
							h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed -= h);
			fundingTxId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m), replaceable: true);
			eventArgs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(fundingTxId, eventArgs.NewlyReceivedCoins.Single().TransactionId);
			Assert.Contains(fundingTxId, wallet.Coins.Select(x => x.TransactionId));

			var fundingBumpTxId = await rpc.BumpFeeAsync(fundingTxId);
			await Task.Delay(2000); // Waits for the funding transaction get to the mempool.
			Assert.Contains(fundingBumpTxId.TransactionId, wallet.Coins.Select(x => x.TransactionId));
			Assert.DoesNotContain(fundingTxId, wallet.Coins.Select(x => x.TransactionId));
			Assert.Single(wallet.Coins.Where(x => x.TransactionId == fundingBumpTxId.TransactionId));

			// Confirm the coin
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

			Assert.Single(wallet.Coins.Where(x => x.Confirmed && x.TransactionId == fundingBumpTxId.TransactionId));
		}
		finally
		{
			bitcoinStore.IndexStore.NewFilter -= Common.Wallet_NewFilterProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	private static MemoryCache CreateMemoryCache()
	{
		return new MemoryCache(new MemoryCacheOptions
		{
			SizeLimit = 1_000,
			ExpirationScanFrequency = TimeSpan.FromSeconds(30)
		});
	}
}
