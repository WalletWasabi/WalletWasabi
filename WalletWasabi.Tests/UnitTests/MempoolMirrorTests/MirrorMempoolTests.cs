using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.MempoolMirrorTests
{
	public class MirrorMempoolTests
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

		[Fact]
		public async Task CanHandleTheSameTxSentManyTimesAsync()
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

				using var k1 = new Key();
				var blockId = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
				var block = await rpc.GetBlockAsync(blockId[0]);
				var coinBaseTx = block.Transactions[0];

				var tx = Transaction.Create(network);
				using var k2 = new Key();
				tx.Inputs.Add(coinBaseTx, 0);
				tx.Outputs.Add(Money.Coins(49.9999m), k2.PubKey.WitHash.GetAddress(network));
				tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
				var valid = tx.Check();

				await rpc.GenerateAsync(101);

				for (int i = 0; i < 5; i++)
				{
					await rpc.SendRawTransactionAsync(tx);

					await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(2));
				}

				var localMempool = mempoolInstance.GetMempool();

				Assert.Single(localMempool);
				Assert.Contains(tx.GetHash(), localMempool.Keys);
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task CanHandleConfirmationAsync()
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

				using var k1 = new Key();
				var blockId = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
				var block = await rpc.GetBlockAsync(blockId[0]);
				var coinBaseTx = block.Transactions[0];

				var tx = Transaction.Create(network);
				using var k2 = new Key();
				tx.Inputs.Add(coinBaseTx, 0);
				tx.Outputs.Add(Money.Coins(49.9999m), k2.PubKey.WitHash.GetAddress(network));
				tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
				var valid = tx.Check();

				await rpc.GenerateAsync(101);

				var rpcMempoolBeforeSend = await rpc.GetRawMempoolAsync();
				var localMempoolBeforeSend = mempoolInstance.GetMempool();
				Assert.Equal(rpcMempoolBeforeSend.Length, localMempoolBeforeSend.Count);

				await rpc.SendRawTransactionAsync(tx);
				await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(2));
				Thread.Sleep(5000);

				var rpcMempoolAfterSend = await rpc.GetRawMempoolAsync();
				var localMempoolAfterSend = mempoolInstance.GetMempool();
				Assert.Equal(rpcMempoolAfterSend.Length, localMempoolAfterSend.Count);
				Assert.Single(localMempoolAfterSend);
				Assert.Single(rpcMempoolAfterSend);

				await rpc.GenerateAsync(1);
				await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(2));

				var rpcMempoolAfterBlockMined = await rpc.GetRawMempoolAsync();
				var localMempoolAfterBlockMined = mempoolInstance.GetMempool();
				Assert.Equal(rpcMempoolAfterBlockMined.Length, localMempoolAfterBlockMined.Count);
				Assert.Empty(rpcMempoolAfterBlockMined);
				Assert.Empty(localMempoolAfterBlockMined);
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}
	}
}
