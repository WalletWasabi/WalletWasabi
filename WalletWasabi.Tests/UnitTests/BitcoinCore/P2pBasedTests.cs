using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
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
		public async Task TrustedNotifierNotifiesBlockAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			try
			{
				var rpc = coreNode.RpcClient;

				var addr = new Key().PubKey.GetSegwitAddress(rpc.Network);
				var notifier = coreNode.TrustedNodeNotifyingBehavior;

				int invs = 0;
				notifier.BlockInv += (s, h) =>
				{
					Interlocked.Increment(ref invs);
				};

				int blocks = 0;
				notifier.Block += (s, h) =>
				{
					Interlocked.Increment(ref blocks);
				};

				var blockNum = 11;
				var blockInvEventAwaiter = new EventsAwaiter<uint256>(
					h => notifier.BlockInv += h,
					h => notifier.BlockInv -= h,
					blockNum);

				var blockEventAwaiter = new EventsAwaiter<Block>(
					h => notifier.Block += h,
					h => notifier.Block -= h,
					blockNum);

				var hashes = (await rpc.GenerateToAddressAsync(blockNum - 1, addr)).ToList();
				// Core does not always send notifications. Wait 1sec and send the notification.
				await Task.Delay(1000);
				hashes.Add((await rpc.GenerateAsync(1)).First());

				var aht = blockInvEventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
				var arrivedBlocks = await blockEventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
				var arrivedHashes = await aht;

				foreach (var hash in arrivedHashes)
				{
					Assert.Contains(hash, hashes);
				}
				foreach (var hash in arrivedBlocks.Select(x => x.GetHash()))
				{
					Assert.Contains(hash, hashes);
				}
			}
			finally
			{
				await coreNode.TryStopAsync();
			}
		}
	}
}
