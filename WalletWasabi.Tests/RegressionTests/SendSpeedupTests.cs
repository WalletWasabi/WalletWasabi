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
public class SendSpeedupTests : IClassFixture<RegTestFixture>
{
	public SendSpeedupTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

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
