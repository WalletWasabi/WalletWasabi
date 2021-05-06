using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.RegressionTests;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.MempoolMirrorTests
{
	public class MirrorMempoolTest
	{
		[Fact]
		public async Task CanCopyMempoolFromRpcAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			using HostedServices services = new();
			using MempoolMirror mempoolMirror = new(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode);
			services.Register<MempoolMirror>(mempoolMirror, "Mempool Mirror");
			try
			{
				var rpc = coreNode.RpcClient;
				var network = rpc.Network;
				var mempoolInstance = services.Get<MempoolMirror>();

				var walletName = "RandomWalletName";
				await rpc.CreateWalletAsync(walletName);

				using var k1 = new Key();
				var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
				var spendAmount2 = new Money(0.0004m, MoneyUnit.BTC);
				await coreNode.RpcClient.GenerateAsync(101);

				var txid = await coreNode.RpcClient.SendToAddressAsync(k1.GetBitcoinSecret(network).GetAddress(ScriptPubKeyType.Segwit), spendAmount);
				var txid2 = await coreNode.RpcClient.SendToAddressAsync(k1.GetBitcoinSecret(network).GetAddress(ScriptPubKeyType.Segwit), spendAmount2);

				await services.StartAllAsync();
				await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(2));
				var localMempool = services.Get<MempoolMirror>().GetMempool();

				Assert.Equal(2, localMempool.Count);
				Assert.Contains(txid, localMempool.Keys);
				Assert.Contains(txid2, localMempool.Keys);
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task CanHandleArrivingTxAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			using HostedServices services = new();
			using MempoolMirror mempoolMirror = new(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode);
			services.Register<MempoolMirror>(mempoolMirror, "Mempool Mirror");
			try
			{
				var rpc = coreNode.RpcClient;
				var network = rpc.Network;
				await services.StartAllAsync();
				var mempoolInstance = services.Get<MempoolMirror>();

				var walletName = "RandomWalletName";
				await rpc.CreateWalletAsync(walletName);

				using var key = new Key();
				var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
				await coreNode.RpcClient.GenerateAsync(101);

				var txid = await coreNode.RpcClient.SendToAddressAsync(key.GetBitcoinSecret(network).GetAddress(ScriptPubKeyType.Segwit), spendAmount);

				await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(2));
				var localMempool = mempoolInstance.GetMempool();

				Assert.Single(localMempool);
				Assert.Contains(txid, localMempool.Keys);
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}
	}
}
