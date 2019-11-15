using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class P2pBasedTests
	{
		[Fact]
		public async Task MempoolNotifiesAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			using var node = await coreNode.CreateNewP2pNodeAsync();
			try
			{
				var rpc = coreNode.RpcClient;
				await rpc.GenerateAsync(101);
				var network = rpc.Network;
				var bitcoinStore = new BitcoinStore();

				var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName());
				await bitcoinStore.InitializeAsync(dir, network);

				node.Behaviors.Add(bitcoinStore.CreateMempoolBehavior());
				node.VersionHandshake();

				var addr = new Key().PubKey.GetSegwitAddress(network);

				var txNum = 10;
				var eventAwaiter = new EventsAwaiter<SmartTransaction>(
					h => bitcoinStore.MempoolService.TransactionReceived += h,
					h => bitcoinStore.MempoolService.TransactionReceived -= h,
					txNum);

				var txTasks = new List<Task<uint256>>();
				var batch = rpc.PrepareBatch();
				for (int i = 0; i < txNum; i++)
				{
					txTasks.Add(batch.SendToAddressAsync(addr, Money.Coins(1)));
				}
				var batchTask = batch.SendBatchAsync();

				var stxs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));

				await batchTask;
				var hashes = await Task.WhenAll(txTasks);
				foreach (var stx in stxs)
				{
					Assert.Contains(stx.GetHash(), hashes);
				}
			}
			finally
			{
				node.Disconnect();
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task TrustedNotifierNotifiesTxAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			try
			{
				var rpc = coreNode.RpcClient;
				await rpc.GenerateAsync(101);
				var network = rpc.Network;

				var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName());

				var addr = new Key().PubKey.GetSegwitAddress(network);
				var notifier = coreNode.TrustedNodeNotifyingBehavior;

				var txNum = 10;
				var txInvEventAwaiter = new EventsAwaiter<uint256>(
					h => notifier.TransactionInv += h,
					h => notifier.TransactionInv -= h,
					txNum);

				var txEventAwaiter = new EventsAwaiter<SmartTransaction>(
					h => notifier.Transaction += h,
					h => notifier.Transaction -= h,
					txNum);

				var txTasks = new List<Task<uint256>>();
				var batch = rpc.PrepareBatch();
				for (int i = 0; i < txNum; i++)
				{
					txTasks.Add(batch.SendToAddressAsync(addr, Money.Coins(1)));
				}
				var batchTask = batch.SendBatchAsync();

				var aht = txInvEventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
				var arrivedTxs = await txEventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
				var arrivedHashes = await aht;

				await batchTask;
				var hashes = await Task.WhenAll(txTasks);
				foreach (var hash in arrivedHashes)
				{
					Assert.Contains(hash, hashes);
				}
				foreach (var hash in arrivedTxs.Select(x => x.GetHash()))
				{
					Assert.Contains(hash, hashes);
				}
			}
			finally
			{
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task BlockNotifierTestsAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			try
			{
				var rpc = coreNode.RpcClient;
				BlockNotifier notifier = coreNode.BlockNotifier;

				// Make we get notification for one block.
				var blockEventAwaiter = new EventAwaiter<Block>(
					h => notifier.OnBlock += h,
					h => notifier.OnBlock -= h);

				var hash = (await rpc.GenerateAsync(1)).First();
				var block = await blockEventAwaiter.WaitAsync(TimeSpan.FromSeconds(7));
				Assert.Equal(hash, block.GetHash());

				// Make sure we get notifications about 10 blocks created the same time.
				var blockNum = 10;
				var blockEventsAwaiter = new EventsAwaiter<Block>(
					h => notifier.OnBlock += h,
					h => notifier.OnBlock -= h,
					blockNum);

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
				var reorgEventsAwaiter = new EventsAwaiter<BlockHeader>(
					h => notifier.OnReorg += h,
					h => notifier.OnReorg -= h,
					reorgNum);
				blockNum = 10;
				blockEventsAwaiter = new EventsAwaiter<Block>(
					h => notifier.OnBlock += h,
					h => notifier.OnBlock -= h,
					newBlockNum);

				var reorgedHashes = hashes.TakeLast(reorgNum).ToArray();
				await rpc.InvalidateBlockAsync(reorgedHashes[0]);
				var newHashes = (await rpc.GenerateAsync(newBlockNum)).ToArray();

				var reorgedHeaders = (await reorgEventsAwaiter.WaitAsync(TimeSpan.FromSeconds(21))).ToArray();
				var newBlocks = (await blockEventsAwaiter.WaitAsync(TimeSpan.FromSeconds(21))).ToArray();

				for (int i = 0; i < reorgedHashes.Length; i++)
				{
					var expected = reorgedHashes[i];
					var actual = reorgedHeaders[i].GetHash();
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
				await coreNode.TryStopAsync();
			}
		}
	}
}
