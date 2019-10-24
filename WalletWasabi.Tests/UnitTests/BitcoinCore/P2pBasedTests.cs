using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using WalletWasabi.Tests.NodeBuilding;
using WalletWasabi.Transactions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class P2pBasedTests
	{
		[Fact]
		public async Task MempoolNotifiesAsync()
		{
			var coreNode = await CoreNode.CreateAsync();
			using var node = await coreNode.CreateP2pNodeAsync();
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

				var stxs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(7));

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
				await coreNode.StopAsync();
			}
		}
	}
}
