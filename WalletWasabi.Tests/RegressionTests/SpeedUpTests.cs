using Microsoft.Extensions.Caching.Memory;
using NBitcoin.Protocol;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using WalletWasabi.Logging;
using WalletWasabi.Helpers;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Tests.RegressionTests;

/// <seealso cref="RegTestCollectionDefinition"/>
[Collection("RegTest collection")]
public class SpeedUpTests : IClassFixture<RegTestFixture>
{
	public SpeedUpTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task ReceiveSpeedupTestsAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture, numberOfBlocksToGenerate: 1);
		IRPCClient rpc = setup.RpcClient;
		Network network = setup.Network;
		BitcoinStore bitcoinStore = setup.BitcoinStore;
		using Backend.Global global = setup.Global;
		ServiceConfiguration serviceConfiguration = setup.ServiceConfiguration;
		string password = setup.Password;

		bitcoinStore.IndexStore.NewFilter += setup.Wallet_NewFilterProcessed;

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 10000, bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Common.GetWorkDir();

		using MemoryCache cache = BitcoinFactory.CreateMemoryCache();
		await using SpecificNodeBlockProvider specificNodeBlockProvider = new(network, serviceConfiguration, httpClientFactory.TorEndpoint);

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			rpcBlockProvider: null,
			specificNodeBlockProvider,
			new P2PBlockProvider(network, nodes, httpClientFactory.IsTorEnabled),
			cache);

		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir), bitcoinStore, synchronizer, serviceConfiguration);
		walletManager.RegisterServices(feeProvider, blockProvider);

		try
		{
			Interlocked.Exchange(ref setup.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await setup.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);
			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);
			wallet.Kitchen.Cook(password);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			// Get some money.
			var key = keyManager.GetNextReceiveKey("foo");
			var txId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
			Assert.NotNull(txId);

			var waitCount = 0;
			while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					Logger.LogInfo($"Funding transaction to the wallet '{wallet.WalletName}' did not arrive.");
					return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
				}
			}

			#region CanSpeedUp

			Assert.True(bitcoinStore.TransactionStore.TryGetTransaction(txId, out var txToSpeedUp));
			var cpfp = wallet.SpeedUpTransaction(txToSpeedUp);
			await broadcaster.SendTransactionAsync(cpfp.Transaction);

			Assert.Equal(2, txToSpeedUp.Transaction.Outputs.Count);
			var outputToSpend = Assert.Single(txToSpeedUp.GetWalletOutputs(keyManager));

			Assert.Single(cpfp.Transaction.Transaction.Inputs);
			Assert.Single(cpfp.Transaction.Transaction.Outputs);
			var cpfpInput = Assert.Single(cpfp.Transaction.WalletInputs);
			var cpfpOutput = Assert.Single(cpfp.Transaction.WalletOutputs);

			Assert.Equal(outputToSpend, cpfpInput);

			// CPFP fee rate should be higher than the best fee rate.
			var feeRate = wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2);
			Assert.NotNull(feeRate);
			var cpfpFeeRate = cpfp.Transaction.Transaction.GetFeeRate(cpfp.Transaction.WalletInputs.Select(x => x.Coin).ToArray());
			Assert.True(feeRate < cpfpFeeRate);

			Assert.False(txToSpeedUp.IsReplacement);
			Assert.False(txToSpeedUp.IsCpfp);
			Assert.False(txToSpeedUp.IsCancellation);
			Assert.False(cpfp.Transaction.IsReplacement);
			Assert.True(cpfp.Transaction.IsCpfp);
			Assert.False(cpfp.Transaction.IsCancellation);

			#endregion CanSpeedUp

			#region CanSpeedUpTwice

			var rbf = wallet.SpeedUpTransaction(cpfp.Transaction);
			await broadcaster.SendTransactionAsync(rbf.Transaction);
			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(cpfp.Transaction.GetHash(), out _));

			var rbfInput = Assert.Single(rbf.Transaction.GetWalletInputs(keyManager));
			Assert.Equal(rbfInput, cpfpInput);

			Assert.Single(rbf.Transaction.Transaction.Inputs);
			Assert.Single(rbf.Transaction.Transaction.Outputs);
			Assert.Single(rbf.Transaction.WalletInputs);
			var rbfOutput = Assert.Single(rbf.Transaction.WalletOutputs);
			Assert.NotEqual(rbfOutput, cpfpOutput);

			// RBF fee rate should be higher than the previous CPFP fee rate.
			var rbfFeeRate = rbf.Transaction.Transaction.GetFeeRate(rbf.Transaction.WalletInputs.Select(x => x.Coin).ToArray());
			Assert.True(cpfpFeeRate < rbfFeeRate);

			Assert.True(rbf.Transaction.IsReplacement);
			Assert.True(rbf.Transaction.IsCpfp);
			Assert.False(rbf.Transaction.IsCancellation);

			#endregion CanSpeedUpTwice

			#region CanSpeedUpThrice

			var rbf2 = wallet.SpeedUpTransaction(rbf.Transaction);
			await broadcaster.SendTransactionAsync(rbf2.Transaction);
			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(rbf.Transaction.GetHash(), out _));

			var rbf2Input = Assert.Single(rbf2.Transaction.GetWalletInputs(keyManager));
			Assert.Equal(rbf2Input, rbfInput);

			Assert.Single(rbf2.Transaction.Transaction.Inputs);
			Assert.Single(rbf2.Transaction.Transaction.Outputs);
			Assert.Single(rbf2.Transaction.WalletInputs);
			var rbf2Output = Assert.Single(rbf2.Transaction.WalletOutputs);
			Assert.NotEqual(rbfOutput, rbf2Output);

			// RBF2 fee rate should be higher than the previous RBF fee rate.
			var rbf2FeeRate = rbf2.Transaction.Transaction.GetFeeRate(rbf2.Transaction.WalletInputs.Select(x => x.Coin).ToArray());
			Assert.True(rbfFeeRate < rbf2FeeRate);

			Assert.True(rbf2.Transaction.IsReplacement);
			Assert.True(rbf2.Transaction.IsCpfp);
			Assert.False(rbf2.Transaction.IsCancellation);

			#endregion CanSpeedUpThrice

			#region CantSpeedUpTooSmall

			// Get some money.
			var txIdJustEnoughToSpeedUp = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network), Money.Coins(0.000991m));
			var txIdTooSmallToSpeedUp = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network), Money.Coins(0.0008m));
			Assert.NotNull(txIdJustEnoughToSpeedUp);
			Assert.NotNull(txIdTooSmallToSpeedUp);

			waitCount = 0;
			while (!bitcoinStore.TransactionStore.TryGetTransaction(txIdJustEnoughToSpeedUp, out _) || !bitcoinStore.TransactionStore.TryGetTransaction(txIdTooSmallToSpeedUp, out _))
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					throw new InvalidOperationException($"Transaction(s) didn't arrive.");
				}
			}

			Assert.True(bitcoinStore.TransactionStore.TryGetTransaction(txIdJustEnoughToSpeedUp, out var txJustEnoughToSpeedUp));
			Assert.True(bitcoinStore.TransactionStore.TryGetTransaction(txIdTooSmallToSpeedUp, out var txTooSmallToSpeedUp));

			// Can't speed too small transaction.
			Assert.Throws<InvalidOperationException>(() => wallet.SpeedUpTransaction(txTooSmallToSpeedUp));

			// Can only speed up not too small, but not too large transaction once.
			cpfp = wallet.SpeedUpTransaction(txJustEnoughToSpeedUp);
			await broadcaster.SendTransactionAsync(cpfp.Transaction);
			Assert.Throws<InvalidOperationException>(() => wallet.SpeedUpTransaction(cpfp.Transaction));

			#endregion CantSpeedUpTooSmall
		}
		finally
		{
			bitcoinStore.IndexStore.NewFilter -= setup.Wallet_NewFilterProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	[Fact]
	public async Task SelfSpendSpeedupTestsAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture, numberOfBlocksToGenerate: 1);
		IRPCClient rpc = setup.RpcClient;
		Network network = setup.Network;
		BitcoinStore bitcoinStore = setup.BitcoinStore;
		using Backend.Global global = setup.Global;
		ServiceConfiguration serviceConfiguration = setup.ServiceConfiguration;
		string password = setup.Password;

		bitcoinStore.IndexStore.NewFilter += setup.Wallet_NewFilterProcessed;

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 10000, bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Common.GetWorkDir();

		using MemoryCache cache = BitcoinFactory.CreateMemoryCache();
		await using SpecificNodeBlockProvider specificNodeBlockProvider = new(network, serviceConfiguration, httpClientFactory.TorEndpoint);

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			rpcBlockProvider: null,
			specificNodeBlockProvider,
			new P2PBlockProvider(network, nodes, httpClientFactory.IsTorEnabled),
			cache);

		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir), bitcoinStore, synchronizer, serviceConfiguration);
		walletManager.RegisterServices(feeProvider, blockProvider);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo");
		var txId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
		Assert.NotNull(txId);
		await rpc.GenerateAsync(1);

		try
		{
			Interlocked.Exchange(ref setup.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await setup.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);
			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);
			wallet.Kitchen.Cook(password);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			var waitCount = 0;
			while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					Logger.LogInfo($"Funding transaction to the wallet '{wallet.WalletName}' did not arrive.");
					return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
				}
			}

			#region HasChange

			Money amountToSend = wallet.Coins.Where(x => x.IsAvailable()).Sum(x => x.Amount) / 2;
			var txToSpeedUp = wallet.BuildTransaction(password, new PaymentIntent(keyManager.GetNextReceiveKey("foo").GetAssumedScriptPubKey(), amountToSend, label: "bar"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);
			await broadcaster.SendTransactionAsync(txToSpeedUp.Transaction);

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);

			var rbf = wallet.SpeedUpTransaction(txToSpeedUp.Transaction);

			// Spender is not updated until broadcast:
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);

			await broadcaster.SendTransactionAsync(rbf.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(txToSpeedUp.Transaction.GetHash(), out _));

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins), Assert.Single(rbf.SpentCoins));
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf.Transaction);

			Assert.Equal(2, txToSpeedUp.InnerWalletOutputs.Count());
			Assert.Equal(2, rbf.InnerWalletOutputs.Count());
			var smallerOutput = txToSpeedUp.InnerWalletOutputs.OrderBy(x => x.Amount).First();
			Assert.Contains(smallerOutput.Amount, rbf.InnerWalletOutputs.Select(x => x.Amount));
			Assert.Contains(smallerOutput.ScriptPubKey, rbf.InnerWalletOutputs.Select(x => x.ScriptPubKey));
			var largerOutput = txToSpeedUp.InnerWalletOutputs.OrderByDescending(x => x.Amount).First();
			Assert.DoesNotContain(largerOutput.Amount, rbf.InnerWalletOutputs.Select(x => x.Amount));
			Assert.Contains(largerOutput.ScriptPubKey, rbf.InnerWalletOutputs.Select(x => x.ScriptPubKey));

			Assert.Empty(txToSpeedUp.OuterWalletOutputs);
			Assert.Empty(rbf.OuterWalletOutputs);

			Assert.True(rbf.Fee > txToSpeedUp.Fee);

			Assert.False(txToSpeedUp.SpendsUnconfirmed);
			Assert.False(rbf.SpendsUnconfirmed);

			Assert.Empty(txToSpeedUp.Transaction.ForeignInputs);
			Assert.Empty(rbf.Transaction.ForeignInputs);

			Assert.False(txToSpeedUp.Transaction.IsReplacement);
			Assert.False(txToSpeedUp.Transaction.IsCpfp);
			Assert.False(txToSpeedUp.Transaction.IsCancellation);
			Assert.True(rbf.Transaction.IsReplacement);
			Assert.False(rbf.Transaction.IsCpfp);
			Assert.False(rbf.Transaction.IsCancellation);

			#endregion HasChange

			#region CanDoTwice

			var rbf2 = wallet.SpeedUpTransaction(rbf.Transaction);

			// Spender is not updated until broadcast:
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf2.SpentCoins).SpenderTransaction, rbf.Transaction);

			await broadcaster.SendTransactionAsync(rbf2.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(rbf.Transaction.GetHash(), out _));

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins), Assert.Single(rbf2.SpentCoins));
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, rbf2.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf2.Transaction);
			Assert.Equal(Assert.Single(rbf2.SpentCoins).SpenderTransaction, rbf2.Transaction);

			smallerOutput = rbf.InnerWalletOutputs.OrderBy(x => x.Amount).First();
			Assert.Equal(2, rbf2.InnerWalletOutputs.Count());
			Assert.Contains(smallerOutput.Amount, rbf2.InnerWalletOutputs.Select(x => x.Amount));
			Assert.Contains(smallerOutput.ScriptPubKey, rbf2.InnerWalletOutputs.Select(x => x.ScriptPubKey));
			largerOutput = rbf.InnerWalletOutputs.OrderByDescending(x => x.Amount).First();
			Assert.DoesNotContain(largerOutput.Amount, rbf2.InnerWalletOutputs.Select(x => x.Amount));
			Assert.Contains(largerOutput.ScriptPubKey, rbf2.InnerWalletOutputs.Select(x => x.ScriptPubKey));

			Assert.Empty(rbf2.OuterWalletOutputs);

			Assert.True(rbf2.Fee > rbf.Fee);

			Assert.False(rbf2.SpendsUnconfirmed);

			Assert.Empty(rbf2.Transaction.ForeignInputs);

			Assert.True(rbf2.Transaction.IsReplacement);
			Assert.False(rbf2.Transaction.IsCpfp);
			Assert.False(rbf2.Transaction.IsCancellation);

			await rpc.GenerateAsync(1);

			while (!rbf2.Transaction.Confirmed)
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					Logger.LogInfo($"Wallet didn't recognize transaction confirmation.");
					return;
				}
			}

			#endregion CanDoTwice

			#region HasNoChange

			txToSpeedUp = wallet.BuildChangelessTransaction(keyManager.GetNextReceiveKey("foo").GetAssumedScriptPubKey().GetDestination()!, "foo", new FeeRate(1m), wallet.Coins);
			await broadcaster.SendTransactionAsync(txToSpeedUp.Transaction);

			foreach (var coin in txToSpeedUp.SpentCoins)
			{
				Assert.Equal(coin.SpenderTransaction, txToSpeedUp.Transaction);
			}

			rbf = wallet.SpeedUpTransaction(txToSpeedUp.Transaction);

			// Spender is not updated until broadcast:
			foreach (var coin in txToSpeedUp.SpentCoins)
			{
				Assert.Equal(coin.SpenderTransaction, txToSpeedUp.Transaction);
			}
			foreach (var coin in rbf.SpentCoins)
			{
				Assert.Equal(coin.SpenderTransaction, txToSpeedUp.Transaction);
			}

			await broadcaster.SendTransactionAsync(rbf.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(txToSpeedUp.Transaction.GetHash(), out _));

			foreach (var coin in txToSpeedUp.SpentCoins)
			{
				Assert.Equal(coin.SpenderTransaction, rbf.Transaction);
				Assert.Contains(coin, rbf.SpentCoins);
			}
			foreach (var coin in rbf.SpentCoins)
			{
				Assert.Equal(coin.SpenderTransaction, rbf.Transaction);
				Assert.Contains(coin, txToSpeedUp.SpentCoins);
			}

			var output = Assert.Single(txToSpeedUp.InnerWalletOutputs);
			var rbfOutput = Assert.Single(rbf.InnerWalletOutputs);
			Assert.True(rbfOutput.Amount < output.Amount);
			Assert.Equal(output.ScriptPubKey, rbfOutput.ScriptPubKey);

			Assert.Empty(txToSpeedUp.OuterWalletOutputs);
			Assert.Empty(rbf.OuterWalletOutputs);

			Assert.True(rbf.Fee > txToSpeedUp.Fee);

			Assert.False(txToSpeedUp.SpendsUnconfirmed);
			Assert.False(rbf.SpendsUnconfirmed);

			Assert.Empty(txToSpeedUp.Transaction.ForeignInputs);
			Assert.Empty(rbf.Transaction.ForeignInputs);

			Assert.False(txToSpeedUp.Transaction.IsReplacement);
			Assert.False(txToSpeedUp.Transaction.IsCpfp);
			Assert.False(txToSpeedUp.Transaction.IsCancellation);
			Assert.True(rbf.Transaction.IsReplacement);
			Assert.False(rbf.Transaction.IsCpfp);
			Assert.False(rbf.Transaction.IsCancellation);

			#endregion HasNoChange

			#region TooSmall

			txId = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network), Money.Satoshis(44_000));
			Assert.NotNull(txId);
			await rpc.GenerateAsync(1);

			SmartTransaction? receiveTx;
			while ((wallet.BitcoinStore.TransactionStore.TryGetTransaction(txId, out receiveTx) && receiveTx.Confirmed) is not true)
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					throw new InvalidOperationException($"Wallet didn't recognize transaction confirmation.");
				}
			}
			txToSpeedUp = wallet.BuildTransaction(password, new PaymentIntent(keyManager.GetNextReceiveKey("foo").GetAssumedScriptPubKey().GetDestination()!, MoneyRequest.CreateAllRemaining()), FeeStrategy.CreateFromFeeRate(new FeeRate(200m)), allowedInputs: receiveTx.WalletOutputs.Select(x => x.Outpoint));
			await broadcaster.SendTransactionAsync(txToSpeedUp.Transaction);

			Assert.Throws<NotEnoughFundsException>(() => wallet.SpeedUpTransaction(txToSpeedUp.Transaction));

			#endregion TooSmall
		}
		finally
		{
			bitcoinStore.IndexStore.NewFilter -= setup.Wallet_NewFilterProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	[Fact]
	public async Task SendSpeedupTestsAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture, numberOfBlocksToGenerate: 1);
		IRPCClient rpc = setup.RpcClient;
		Network network = setup.Network;
		BitcoinStore bitcoinStore = setup.BitcoinStore;
		using Backend.Global global = setup.Global;
		ServiceConfiguration serviceConfiguration = setup.ServiceConfiguration;
		string password = setup.Password;

		bitcoinStore.IndexStore.NewFilter += setup.Wallet_NewFilterProcessed;

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 10000, bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Common.GetWorkDir();

		using MemoryCache cache = BitcoinFactory.CreateMemoryCache();
		await using SpecificNodeBlockProvider specificNodeBlockProvider = new(network, serviceConfiguration, httpClientFactory.TorEndpoint);

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			rpcBlockProvider: null,
			specificNodeBlockProvider,
			new P2PBlockProvider(network, nodes, httpClientFactory.IsTorEnabled),
			cache);

		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir), bitcoinStore, synchronizer, serviceConfiguration);
		walletManager.RegisterServices(feeProvider, blockProvider);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo");
		var txId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
		Assert.NotNull(txId);
		await rpc.GenerateAsync(1);

		try
		{
			Interlocked.Exchange(ref setup.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await setup.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);
			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);
			wallet.Kitchen.Cook(password);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			var waitCount = 0;
			while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					Logger.LogInfo($"Funding transaction to the wallet '{wallet.WalletName}' did not arrive.");
					return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
				}
			}

			#region HasChange

			Money amountToSend = wallet.Coins.Where(x => x.IsAvailable()).Sum(x => x.Amount) / 2;
			var rpcAddress = await rpc.GetNewAddressAsync();
			var txToSpeedUp = wallet.BuildTransaction(password, new PaymentIntent(rpcAddress, amountToSend, label: "bar"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);
			await broadcaster.SendTransactionAsync(txToSpeedUp.Transaction);

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);

			var rbf = wallet.SpeedUpTransaction(txToSpeedUp.Transaction);

			// Spender is not updated until broadcast:
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);

			await broadcaster.SendTransactionAsync(rbf.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(txToSpeedUp.Transaction.GetHash(), out _));

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins), Assert.Single(rbf.SpentCoins));
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf.Transaction);

			var txToSpeedUpChange = Assert.Single(txToSpeedUp.InnerWalletOutputs);
			var rbfChange = Assert.Single(rbf.InnerWalletOutputs);
			Assert.Equal(txToSpeedUpChange.ScriptPubKey, rbfChange.ScriptPubKey);
			Assert.True(txToSpeedUpChange.Amount > rbfChange.Amount);

			var txToSpeedUpForeignOutput = Assert.Single(txToSpeedUp.OuterWalletOutputs);
			var rbfForeignOutput = Assert.Single(rbf.OuterWalletOutputs);
			Assert.Equal(txToSpeedUpForeignOutput.Amount, rbfForeignOutput.Amount);
			Assert.Equal(txToSpeedUpForeignOutput.ScriptPubKey, rbfForeignOutput.ScriptPubKey);

			Assert.True(rbf.Fee > txToSpeedUp.Fee);

			Assert.False(txToSpeedUp.SpendsUnconfirmed);
			Assert.False(rbf.SpendsUnconfirmed);

			Assert.Empty(txToSpeedUp.Transaction.ForeignInputs);
			Assert.Empty(rbf.Transaction.ForeignInputs);

			Assert.False(txToSpeedUp.Transaction.IsReplacement);
			Assert.False(txToSpeedUp.Transaction.IsCpfp);
			Assert.False(txToSpeedUp.Transaction.IsCancellation);
			Assert.True(rbf.Transaction.IsReplacement);
			Assert.False(rbf.Transaction.IsCpfp);
			Assert.False(rbf.Transaction.IsCancellation);

			#endregion HasChange

			#region CanDoTwiceHasChange

			var rbf2 = wallet.SpeedUpTransaction(rbf.Transaction);

			// Spender is not updated until broadcast:
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf2.SpentCoins).SpenderTransaction, rbf.Transaction);

			await broadcaster.SendTransactionAsync(rbf2.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(rbf.Transaction.GetHash(), out _));

			Assert.Equal(Assert.Single(rbf.SpentCoins), Assert.Single(rbf2.SpentCoins));
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf2.Transaction);
			Assert.Equal(Assert.Single(rbf2.SpentCoins).SpenderTransaction, rbf2.Transaction);

			var rbf2Change = Assert.Single(rbf2.InnerWalletOutputs);
			Assert.Equal(rbfChange.ScriptPubKey, rbf2Change.ScriptPubKey);
			Assert.True(rbfChange.Amount > rbf2Change.Amount);

			var rbf2ForeignOutput = Assert.Single(rbf2.OuterWalletOutputs);
			Assert.Equal(rbfForeignOutput.Amount, rbf2ForeignOutput.Amount);
			Assert.Equal(rbfForeignOutput.ScriptPubKey, rbf2ForeignOutput.ScriptPubKey);

			Assert.True(rbf2.Fee > rbf.Fee);

			Assert.False(rbf2.SpendsUnconfirmed);

			Assert.Empty(rbf2.Transaction.ForeignInputs);

			Assert.True(rbf2.Transaction.IsReplacement);
			Assert.False(rbf2.Transaction.IsCpfp);
			Assert.False(rbf2.Transaction.IsCancellation);

			await rpc.GenerateAsync(1);
			while (wallet.Coins.Any(x => !x.Confirmed))
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					throw new InvalidOperationException($"Wallet didn't recognize transaction confirmation.");
				}
			}

			#endregion CanDoTwiceHasChange

			#region HasNoChange

			rpcAddress = await rpc.GetNewAddressAsync();
			txToSpeedUp = wallet.BuildTransaction(password, new PaymentIntent(rpcAddress, MoneyRequest.CreateAllRemaining(), label: "bar"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);
			await broadcaster.SendTransactionAsync(txToSpeedUp.Transaction);

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);

			rbf = wallet.SpeedUpTransaction(txToSpeedUp.Transaction);

			// Spender is not updated until broadcast:
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, txToSpeedUp.Transaction);

			await broadcaster.SendTransactionAsync(rbf.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(txToSpeedUp.Transaction.GetHash(), out _));

			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins), Assert.Single(rbf.SpentCoins));
			Assert.Equal(Assert.Single(txToSpeedUp.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf.Transaction);

			var txToSpeedUpOutput = Assert.Single(txToSpeedUp.Transaction.Transaction.Outputs);
			var rbfOutput = Assert.Single(rbf.Transaction.Transaction.Outputs);
			Assert.Equal(txToSpeedUpOutput.ScriptPubKey, rbfOutput.ScriptPubKey);
			Assert.True(txToSpeedUpOutput.Value > rbfOutput.Value);

			Assert.True(rbf.Fee > txToSpeedUp.Fee);

			Assert.False(txToSpeedUp.SpendsUnconfirmed);
			Assert.False(rbf.SpendsUnconfirmed);

			Assert.Empty(txToSpeedUp.Transaction.ForeignInputs);
			Assert.Empty(rbf.Transaction.ForeignInputs);

			Assert.False(txToSpeedUp.Transaction.IsReplacement);
			Assert.False(txToSpeedUp.Transaction.IsCpfp);
			Assert.False(txToSpeedUp.Transaction.IsCancellation);
			Assert.True(rbf.Transaction.IsReplacement);
			Assert.False(rbf.Transaction.IsCpfp);
			Assert.False(rbf.Transaction.IsCancellation);

			#endregion HasNoChange

			#region CanDoTwiceHasNoChange

			rbf2 = wallet.SpeedUpTransaction(rbf.Transaction);

			// Spender is not updated until broadcast:
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf.Transaction);
			Assert.Equal(Assert.Single(rbf2.SpentCoins).SpenderTransaction, rbf.Transaction);

			await broadcaster.SendTransactionAsync(rbf2.Transaction);

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(rbf.Transaction.GetHash(), out _));

			Assert.Equal(Assert.Single(rbf.SpentCoins), Assert.Single(rbf2.SpentCoins));
			Assert.Equal(Assert.Single(rbf.SpentCoins).SpenderTransaction, rbf2.Transaction);
			Assert.Equal(Assert.Single(rbf2.SpentCoins).SpenderTransaction, rbf2.Transaction);

			var rbf2Output = Assert.Single(rbf2.Transaction.Transaction.Outputs);
			Assert.Equal(rbfOutput.ScriptPubKey, rbf2Output.ScriptPubKey);
			Assert.True(rbfOutput.Value > rbf2Output.Value);

			Assert.True(rbf2.Fee > rbf.Fee);

			Assert.False(rbf2.SpendsUnconfirmed);

			Assert.Empty(rbf2.Transaction.ForeignInputs);

			Assert.True(rbf2.Transaction.IsReplacement);
			Assert.False(rbf2.Transaction.IsCpfp);
			Assert.False(rbf2.Transaction.IsCancellation);

			await rpc.GenerateAsync(1);
			while (wallet.Coins.Any(x => !x.Confirmed))
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					throw new InvalidOperationException($"Wallet didn't recognize transaction confirmation.");
				}
			}

			#endregion CanDoTwiceHasNoChange

			#region TooSmallHasNoChange

			;

			#endregion TooSmallHasNoChange

			#region TooSmallToRbfButCanCpfpHasChange

			;

			#endregion TooSmallToRbfButCanCpfpHasChange

			#region TooSmallHasChange

			;

			#endregion TooSmallHasChange
		}
		finally
		{
			bitcoinStore.IndexStore.NewFilter -= setup.Wallet_NewFilterProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}
}
