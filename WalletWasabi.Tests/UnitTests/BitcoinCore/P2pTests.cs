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
	public class P2pTests
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

				var addr = await rpc.GetNewAddressAsync();

				for (int i = 0; i < 10; i++)
				{
					var valueChangedEventAwaiter = new EventAwaiter<SmartTransaction>(
							h => bitcoinStore.MempoolService.TransactionReceived += h,
							h => bitcoinStore.MempoolService.TransactionReceived -= h);

					var txid = await rpc.SendToAddressAsync(addr, Money.Coins(1));
					Assert.NotNull(txid);

					using var cts = new CancellationTokenSource(1000);
					var stx = await valueChangedEventAwaiter.Task.WithCancellation(cts.Token);

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
