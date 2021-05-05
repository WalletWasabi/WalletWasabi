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
			try
			{
				using MempoolMirror mempoolMirror = new MempoolMirror(TimeSpan.FromSeconds(1), coreNode.RpcClient, coreNode.P2pNode);
				services.Register<MempoolMirror>(mempoolMirror, "Mempool Mirror");
				await services.StartAllAsync();

				var network = coreNode.RpcClient.Network;

				using var k1 = new Key();
				using var k2 = new Key();
				var blockId = await coreNode.RpcClient.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
				var block = await coreNode.RpcClient.GetBlockAsync(blockId[0]);
				var coinBaseTx = block.Transactions[0];

				var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
				var minerFee = new Money(0.000234m, MoneyUnit.BTC);
				var txInAmount = coinBaseTx;
				var changeAmount = txInAmount.Outputs.FirstOrDefault().Value - spendAmount - minerFee;

				var tx = Transaction.Create(network);
				tx.Inputs.Add(txInAmount, 0);
				tx.Outputs.Add(spendAmount, k2.PubKey.WitHash.GetAddress(network));
				tx.Outputs.Add(changeAmount, k1.PubKey.WitHash.GetAddress(network));
				tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
				var valid = tx.Check();

				await coreNode.RpcClient.GenerateToAddressAsync(101, k1.PubKey.WitHash.GetAddress(network));

				var txid = await coreNode.RpcClient.SendRawTransactionAsync(tx);

				await services.Get<MempoolMirror>().TriggerAndWaitRoundAsync(TimeSpan.FromMilliseconds(5000));

				var localMempool = services.Get<MempoolMirror>().GetMempool();
				Console.WriteLine(localMempool.Count);

				Assert.Contains(txid, localMempool.Keys);
				Assert.Single(localMempool);
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
			finally
			{
				await services.StopAllAsync();
				await coreNode.TryStopAsync();
			}
		}
	}
}
