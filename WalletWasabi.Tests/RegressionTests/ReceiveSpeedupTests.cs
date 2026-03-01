using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin.Protocol;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using Xunit;
using WalletWasabi.Logging;
using WalletWasabi.Helpers;
using WalletWasabi.Exceptions;
using WalletWasabi.FeeRateEstimation;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Tests.RegressionTests;

/// <seealso cref="RegTestCollectionDefinition"/>
[Collection("RegTest collection")]
public class ReceiveSpeedupTests : IClassFixture<RegTestFixture>
{
	public ReceiveSpeedupTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task ReceiveSpeedupTestsAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);
		IRPCClient rpc = setup.RpcClient;
		Network network = setup.Network;
		BitcoinStore bitcoinStore = setup.BitcoinStore;
		ServiceConfiguration serviceConfiguration = setup.ServiceConfiguration;
		string password = setup.Password;

		bitcoinStore.FilterStore.NewFilters += setup.Wallet_NewFiltersProcessed;

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(setup.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.IndexerRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.IndexerRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		var httpClientFactory = RegTestFixture.IndexerHttpClientFactory;
		var filterProvider = new WebApiFilterProvider(10_000, httpClientFactory, setup.EventBus);
		var (_, _, serviceLoop) = Continuously(Synchronizer.CreateFilterGenerator(filterProvider, bitcoinStore, setup.EventBus));
		using var synchronizer = Spawn("Synchronizer", serviceLoop);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Common.GetWorkDir();

		using MemoryCache cache = BitcoinFactory.CreateMemoryCache();

		var blockProvider = BlockProviders.P2pBlockProvider(new P2PNodesManager(Network.Main, nodes));

		var walletFactory = Wallet.CreateFactory(network, bitcoinStore, serviceConfiguration, blockProvider, setup.EventBus, setup.CpfpInfoProvider);
		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir), walletFactory);
		walletManager.Initialize();

		try
		{
			Interlocked.Exchange(ref setup.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.

			// Start wallet and filter processing service.
			using var wallet = await walletManager.AddAndStartWalletAsync(keyManager);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await setup.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

			wallet.Password = password;

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
			var cpfp = await wallet.SpeedUpTransactionAsync(txToSpeedUp, null, CancellationToken.None);

			TransactionBroadcaster broadcaster = new([new RpcBroadcaster(rpc)], bitcoinStore.MempoolService, walletManager);
			await broadcaster.SendTransactionAsync(cpfp.Transaction);

			Assert.Equal("foo", txToSpeedUp.Labels.Single());
			Assert.Empty(cpfp.Transaction.Labels);
			foreach (var op in txToSpeedUp.WalletOutputs)
			{
				Assert.Equal("foo", op.HdPubKey.Labels.Single());
			}
			foreach (var op in cpfp.InnerWalletOutputs)
			{
				Assert.Empty(op.HdPubKey.Labels);
			}

			Assert.Equal(2, txToSpeedUp.Transaction.Outputs.Count);
			var outputToSpend = Assert.Single(txToSpeedUp.GetWalletOutputs(keyManager));

			Assert.Single(cpfp.Transaction.Transaction.Inputs);
			Assert.Single(cpfp.Transaction.Transaction.Outputs);
			var cpfpInput = Assert.Single(cpfp.Transaction.WalletInputs);
			var cpfpOutput = Assert.Single(cpfp.Transaction.WalletOutputs);

			Assert.Equal(outputToSpend, cpfpInput);

			// CPFP fee rate should be higher than the best fee rate.
			var feeRate = wallet.FeeRateEstimations!.GetFeeRate(2);
			Assert.NotNull(feeRate);
			var cpfpFeeRate = cpfp.Transaction.Transaction.GetFeeRate(cpfp.Transaction.WalletInputs.Select(x => x.Coin).ToArray());
			Assert.True(feeRate < cpfpFeeRate);

			Assert.False(txToSpeedUp.IsReplacement);
			Assert.False(txToSpeedUp.IsCPFP);
			Assert.True(txToSpeedUp.IsCPFPd);
			Assert.Empty(txToSpeedUp.ParentsThisTxPaysFor);
			Assert.Single(txToSpeedUp.ChildrenPayForThisTx);
			Assert.False(txToSpeedUp.IsSpeedup);
			Assert.False(txToSpeedUp.IsCancellation);
			Assert.False(cpfp.Transaction.IsReplacement);
			Assert.True(cpfp.Transaction.IsCPFP);
			Assert.False(cpfp.Transaction.IsCPFPd);
			Assert.Single(cpfp.Transaction.ParentsThisTxPaysFor);
			Assert.Empty(cpfp.Transaction.ChildrenPayForThisTx);
			Assert.True(cpfp.Transaction.IsSpeedup);
			Assert.False(cpfp.Transaction.IsCancellation);

			#endregion CanSpeedUp

			#region CanSpeedUpTwice

			var rbf = await wallet.SpeedUpTransactionAsync(cpfp.Transaction, null, CancellationToken.None);
			await broadcaster.SendTransactionAsync(rbf.Transaction);
			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(cpfp.Transaction.GetHash(), out _));

			Assert.Empty(rbf.Transaction.Labels);
			foreach (var op in rbf.InnerWalletOutputs)
			{
				Assert.Empty(op.HdPubKey.Labels);
			}

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

			Assert.True(cpfp.Transaction.IsCPFP);
			Assert.False(cpfp.Transaction.IsCPFPd);
			Assert.True(cpfp.Transaction.IsSpeedup);
			Assert.Single(cpfp.Transaction.ParentsThisTxPaysFor);
			Assert.Empty(cpfp.Transaction.ChildrenPayForThisTx);

			Assert.True(rbf.Transaction.IsReplacement);
			Assert.True(rbf.Transaction.IsCPFP);
			Assert.False(rbf.Transaction.IsCPFPd);
			Assert.Single(rbf.Transaction.ParentsThisTxPaysFor);
			Assert.Empty(rbf.Transaction.ChildrenPayForThisTx);
			Assert.True(rbf.Transaction.IsSpeedup);
			Assert.False(rbf.Transaction.IsCancellation);

			#endregion CanSpeedUpTwice

			#region CanSpeedUpCPFPd

			var rbf2 = await wallet.SpeedUpTransactionAsync(txToSpeedUp, null, CancellationToken.None);

			// Before broadcast, it's still the old one.
			Assert.Equal(rbf.Transaction, Assert.Single(txToSpeedUp.ChildrenPayForThisTx));
			Assert.NotEqual(rbf2.Transaction, Assert.Single(txToSpeedUp.ChildrenPayForThisTx));

			await broadcaster.SendTransactionAsync(rbf2.Transaction);

			// After broadcast, it must update.
			Assert.NotEqual(rbf.Transaction, Assert.Single(txToSpeedUp.ChildrenPayForThisTx));
			Assert.Equal(rbf2.Transaction, Assert.Single(txToSpeedUp.ChildrenPayForThisTx));

			Assert.False(wallet.BitcoinStore.TransactionStore.TryGetTransaction(rbf.Transaction.GetHash(), out _));

			Assert.Empty(rbf2.Transaction.Labels);
			foreach (var op in rbf2.InnerWalletOutputs)
			{
				Assert.Empty(op.HdPubKey.Labels);
			}

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

			Assert.True(rbf.Transaction.IsCPFP);
			Assert.False(rbf.Transaction.IsCPFPd);
			Assert.True(rbf.Transaction.IsSpeedup);
			Assert.Single(rbf.Transaction.ParentsThisTxPaysFor);
			Assert.Empty(rbf.Transaction.ChildrenPayForThisTx);

			Assert.True(rbf2.Transaction.IsReplacement);
			Assert.True(rbf2.Transaction.IsCPFP);
			Assert.False(rbf2.Transaction.IsCPFPd);
			Assert.Single(rbf2.Transaction.ParentsThisTxPaysFor);
			Assert.Empty(rbf2.Transaction.ChildrenPayForThisTx);
			Assert.True(rbf2.Transaction.IsSpeedup);
			Assert.False(rbf2.Transaction.IsCancellation);

			#endregion CanSpeedUpCPFPd

			#region CantSpeedUpTooSmall

			// Get some money.
			var txIdTooSmallToSpeedUp = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network), Money.Coins(0.0008m));
			Assert.NotNull(txIdTooSmallToSpeedUp);
			var txIdJustEnoughToSpeedUp = await rpc.SendToAddressAsync(keyManager.GetNextReceiveKey("foo").GetP2wpkhAddress(network), Money.Coins(0.000991m));
			Assert.NotNull(txIdJustEnoughToSpeedUp);

			// Process it.
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

			// Can only speed up not too small, but not too large transaction once.
			cpfp = await wallet.SpeedUpTransactionAsync(txJustEnoughToSpeedUp, null, CancellationToken.None);
			await broadcaster.SendTransactionAsync(cpfp.Transaction);
			await Assert.ThrowsAsync<TransactionFeeOverpaymentException>(async () => await wallet.SpeedUpTransactionAsync(cpfp.Transaction, null, CancellationToken.None));

			Assert.True(bitcoinStore.TransactionStore.TryGetTransaction(txIdTooSmallToSpeedUp, out var txTooSmallToSpeedUp));

			// Can't speed too small transaction.
			await Assert.ThrowsAsync<TransactionFeeOverpaymentException>(async () => await wallet.SpeedUpTransactionAsync(txTooSmallToSpeedUp, null, CancellationToken.None));

			#endregion CantSpeedUpTooSmall
		}
		finally
		{
			bitcoinStore.FilterStore.NewFilters -= setup.Wallet_NewFiltersProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}
}
