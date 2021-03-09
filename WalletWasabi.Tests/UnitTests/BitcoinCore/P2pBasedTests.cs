using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
	[Collection("Serial unit tests collection")]
	public class P2pBasedTests
	{
		[Fact]
		public async Task MempoolNotifiesAsync()
		{
			using HostedServices services = new();
			CoreNode coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync();

			using var node = await coreNode.CreateNewP2pNodeAsync();
			try
			{
				string dir = Common.GetWorkDir();
				var network = coreNode.Network;
				var rpc = coreNode.RpcClient;

				var walletName = "wallet.dat";
				await rpc.CreateWalletAsync(walletName);

				await using IndexStore indexStore = new(Path.Combine(dir, "indexStore"), network, new SmartHeaderChain());
				await using AllTransactionStore transactionStore = new(Path.Combine(dir, "transactionStore"), network);
				MempoolService mempoolService = new();
				FileSystemBlockRepository blocks = new(Path.Combine(dir, "blocks"), network);

				// Construct BitcoinStore.
				await using BitcoinStore bitcoinStore = new(indexStore, transactionStore, mempoolService, blocks);
				await bitcoinStore.InitializeAsync();

				await rpc.GenerateAsync(blockCount: 101);

				node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());
				node.VersionHandshake();

				using Key k = new Key();
				BitcoinWitPubKeyAddress address = k.PubKey.GetSegwitAddress(network);

				// Number of transactions to send.
				const int TransactionsCount = 10;

				EventsAwaiter<SmartTransaction> eventAwaiter = new(
					subscribe: h => mempoolService.TransactionReceived += h,
					unsubscribe: h => mempoolService.TransactionReceived -= h,
					count: TransactionsCount);

				List<Task<uint256>> txHashesList = new();
				IRPCClient rpcBatch = rpc.PrepareBatch();

				// Add to the batch 10 RPC commands: Send 1 coin to the same address.
				for (int i = 0; i < TransactionsCount; i++)
				{
					txHashesList.Add(rpcBatch.SendToAddressAsync(address, Money.Coins(1)));
				}

				// Publish the RPC batch.
				Task rpcBatchTask = rpcBatch.SendBatchAsync();

				// Wait until the mempool service receives all the sent transactions.
				IEnumerable<SmartTransaction> mempoolSmartTxs = await eventAwaiter.WaitAsync(TimeSpan.FromMinutes(2));

				await rpcBatchTask;

				// Collect all the transaction hashes of the sent transactions.
				uint256[] hashes = await Task.WhenAll(txHashesList);

				// Check that all the received transaction hashes are in the set of sent transaction hashes.
				foreach (SmartTransaction tx in mempoolSmartTxs)
				{
					Assert.Contains(tx.GetHash(), hashes);
				}
			}
			finally
			{
				await services.StopAllAsync();
				node.Disconnect();
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task TrustedNotifierNotifiesTxAsync()
		{
			using HostedServices services = new();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync();
			try
			{
				var rpc = coreNode.RpcClient;
				var walletName = "wallet.dat";
				await rpc.CreateWalletAsync(walletName);

				await rpc.GenerateAsync(101);
				var network = rpc.Network;

				var dir = Common.GetWorkDir();

				using Key k = new Key();
				var addr = k.PubKey.GetSegwitAddress(network);
				var notifier = coreNode.MempoolService;

				var txNum = 10;
				EventsAwaiter<SmartTransaction> txEventAwaiter = new(
					h => notifier.TransactionReceived += h,
					h => notifier.TransactionReceived -= h,
					txNum);

				List<Task<uint256>> txTasks = new();
				var batch = rpc.PrepareBatch();
				for (int i = 0; i < txNum; i++)
				{
					txTasks.Add(batch.SendToAddressAsync(addr, Money.Coins(1)));
				}
				var batchTask = batch.SendBatchAsync();

				var arrivedTxs = await txEventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));

				await batchTask;
				var hashes = await Task.WhenAll(txTasks);
				foreach (var hash in arrivedTxs.Select(x => x.GetHash()))
				{
					Assert.Contains(hash, hashes);
				}
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task BlockNotifierTestsAsync()
		{
			using HostedServices services = new();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync();
			try
			{
				var rpc = coreNode.RpcClient;
				var walletName = "wallet.dat";
				await rpc.CreateWalletAsync(walletName);

				BlockNotifier notifier = services.FirstOrDefault<BlockNotifier>();

				// Make sure we get notification for one block.
				EventAwaiter<Block> blockEventAwaiter = new(h => notifier.OnBlock += h, h => notifier.OnBlock -= h);

				var hash = (await rpc.GenerateAsync(1)).First();
				var block = await blockEventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
				Assert.Equal(hash, block.GetHash());

				// Make sure we get notifications about 10 blocks created at the same time.
				var blockNum = 10;
				EventsAwaiter<Block> blockEventsAwaiter = new(h => notifier.OnBlock += h, h => notifier.OnBlock -= h, blockNum);

				var hashes = (await rpc.GenerateAsync(blockNum)).ToArray();

				var arrivedBlocks = (await blockEventsAwaiter.WaitAsync(TimeSpan.FromSeconds(21))).ToArray();

				for (int i = 0; i < hashes.Length; i++)
				{
					var expected = hashes[i];
					var actual = arrivedBlocks[i].GetHash();
					Assert.Equal(expected, actual);
				}

				// Make sure we get reorg notifications.
				var reorgNum = 3;
				var newBlockNum = reorgNum + 1;
				EventsAwaiter<uint256> reorgEventsAwaiter = new(h => notifier.OnReorg += h, h => notifier.OnReorg -= h, reorgNum);
				blockEventsAwaiter = new(
					h => notifier.OnBlock += h,
					h => notifier.OnBlock -= h,
					newBlockNum);

				var reorgedHashes = hashes.TakeLast(reorgNum).ToArray();
				await rpc.InvalidateBlockAsync(reorgedHashes[0]);
				var newHashes = (await rpc.GenerateAsync(newBlockNum)).ToArray();

				var reorgedHeaders = (await reorgEventsAwaiter.WaitAsync(TimeSpan.FromSeconds(21))).ToArray();
				var newBlocks = (await blockEventsAwaiter.WaitAsync(TimeSpan.FromSeconds(21))).ToArray();

				reorgedHashes = reorgedHashes.Reverse().ToArray();
				for (int i = 0; i < reorgedHashes.Length; i++)
				{
					var expected = reorgedHashes[i];
					var actual = reorgedHeaders[i];
					Assert.Equal(expected, actual);
				}

				for (int i = 0; i < newHashes.Length; i++)
				{
					var expected = newHashes[i];
					var actual = newBlocks[i].GetHash();
					Assert.Equal(expected, actual);
				}
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}
	}
}
