using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tests.NodeBuilding;
using WalletWasabi.Transactions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class P2pBasedTests
	{
		[Fact]
		public async Task MempoolWorksAsync()
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

				for (int i = 0; i < 10; i++)
				{
					var eventAwaiter = new EventAwaiter<SmartTransaction>(
							h => bitcoinStore.MempoolService.TransactionReceived += h,
							h => bitcoinStore.MempoolService.TransactionReceived -= h);

					var txid = await rpc.SendToAddressAsync(addr, Money.Coins(1));
					Assert.NotNull(txid);

					using var cts = new CancellationTokenSource(1000);
					var stx = await eventAwaiter.Task.WithCancellation(cts.Token);

					Assert.Equal(txid, stx.GetHash());
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
