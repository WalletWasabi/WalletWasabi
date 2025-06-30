using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Protocol;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using Xunit;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Extensions;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Tests.RegressionTests;

/// <seealso cref="RegTestCollectionDefinition"/>
[Collection("RegTest collection")]
public class ReplaceByFeeTxTest : IClassFixture<RegTestFixture>
{
	public ReplaceByFeeTxTest(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task ReplaceByFeeTxTestAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);
		IRPCClient rpc = setup.RpcClient;
		Network network = setup.Network;
		BitcoinStore bitcoinStore = setup.BitcoinStore;
		ServiceConfiguration serviceConfiguration = setup.ServiceConfiguration;
		string password = setup.Password;

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(setup.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.IndexerRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.IndexerRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		var filterProvider = new WebApiFilterProvider(10_000, RegTestFixture.IndexerHttpClientFactory, setup.EventBus);
		using var synchronizer = Spawn("Synchronizer", Continuously(Synchronizer.CreateFilterGenerator(filterProvider, bitcoinStore, setup.EventBus)));

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Common.GetWorkDir();

		var blockProvider = BlockProviders.P2pBlockProvider(new P2PNodesManager(Network.Main, nodes));

		WalletFactory walletFactory = new(network, bitcoinStore, serviceConfiguration, blockProvider, setup.EventBus, setup.CpfpInfoProvider);
		using Wallet wallet = walletFactory.CreateAndInitialize(keyManager);

		wallet.NewFiltersProcessed += setup.Wallet_NewFiltersProcessed;

		Assert.Empty(wallet.Coins);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo label");

		try
		{
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.

			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
			{
				await wallet.StartAsync(cts.Token); // Initialize wallet service with filter processing.
			}

			// Wait until the filter for block containing our previous transaction was processed.
			var blockCount = await rpc.GetBlockCountAsync();
			await setup.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

			Assert.Empty(wallet.Coins);

			var tx0Id = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m), replaceable: true);
			while (!wallet.Coins.Any())
			{
				await Task.Delay(500); // Waits for the funding transaction get to the mempool.
			}

			Assert.Single(wallet.Coins);
			Assert.True(wallet.Coins.First().Transaction.IsRBF());

			var bfr = await rpc.BumpFeeAsync(tx0Id);
			var tx1Id = bfr.TransactionId;
			await Task.Delay(2000); // Waits for the replacement transaction get to the mempool.
			Assert.Single(wallet.Coins);
			Assert.True(wallet.Coins.First().Transaction.IsRBF());
			Assert.Equal(tx1Id, wallet.Coins.First().TransactionId);

			bfr = await rpc.BumpFeeAsync(tx1Id);
			var tx2Id = bfr.TransactionId;
			await Task.Delay(2000); // Waits for the replacement transaction get to the mempool.
			Assert.Single(wallet.Coins);
			Assert.True(wallet.Coins.First().Transaction.IsRBF());
			Assert.Equal(tx2Id, wallet.Coins.First().TransactionId);

			Interlocked.Exchange(ref setup.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			await setup.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

			var coin = Assert.Single(wallet.Coins);
			Assert.True(coin.Confirmed);
			Assert.False(coin.Transaction.IsRBF());
			Assert.Equal(tx2Id, coin.TransactionId);
		}
		finally
		{
			await wallet.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}
}
