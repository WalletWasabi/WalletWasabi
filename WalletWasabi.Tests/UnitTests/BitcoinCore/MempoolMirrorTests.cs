using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

/// <summary>
/// Tests for <see cref="MempoolMirror"/>.
/// </summary>
public class MempoolMirrorTests
{
	/// <summary>
	/// Verifies that <see cref="MempoolMirror.GetSpenderTransactions(IEnumerable{OutPoint})"/>
	/// returns correct distinct transactions from the mirrored mempool.
	/// </summary>
	[Fact]
	public void GetSpenderTransactions()
	{
		Mock<IRPCClient> mockRpc = new(MockBehavior.Strict);
		using MempoolMirror mempoolMirror = new(period: TimeSpan.FromSeconds(2), mockRpc.Object, node: null!);

		// Empty mirrored mempool & no transaction outputs.
		{
			IEnumerable<OutPoint> txOuts = Enumerable.Empty<OutPoint>();
			IEnumerable<Transaction> transactions = mempoolMirror.GetSpenderTransactions(txOuts);

			Assert.Empty(transactions);
		}

		// Empty mirrored mempool & a single transaction output.
		{
			OutPoint[] txOuts = new[] { new OutPoint(uint256.One, 1) };
			IEnumerable<Transaction> transactions = mempoolMirror.GetSpenderTransactions(txOuts);

			Assert.Empty(transactions);
		}

		Transaction tx1;
		OutPoint tx1prevOut1;
		OutPoint tx1prevOut2;
		TxOut tx1Out1;

		// Add a single transaction to the mirrored mempool.
		{
			tx1 = Network.Main.CreateTransaction();
			tx1prevOut1 = new(hashIn: new uint256(0x1), nIn: 0);
			tx1.Inputs.Add(new TxIn(tx1prevOut1));

			tx1prevOut2 = new(hashIn: new uint256(0x2), nIn: 0);
			tx1.Inputs.Add(new TxIn(tx1prevOut2));

			tx1Out1 = new TxOut(Money.Coins(0.1m), scriptPubKey: Script.Empty);
			tx1.Outputs.Add(tx1Out1);

			// Add the transaction.
			Assert.Equal(1, mempoolMirror.AddTransactions(tx1));
		}

		// Mirrored mempool with tx1 & a non-existent prevOut.
		{
			OutPoint madeUpPrevOut = new(hashIn: new uint256(0x77777), nIn: 0);
			IReadOnlySet<Transaction> transactions = mempoolMirror.GetSpenderTransactions(new[] { madeUpPrevOut });

			Assert.Empty(transactions);
		}

		// Mirrored mempool with tx1 & existing tx1prevOut1.
		{
			IReadOnlySet<Transaction> transactions = mempoolMirror.GetSpenderTransactions(new[] { tx1prevOut1 });

			Transaction actualTx = Assert.Single(transactions);
			Assert.Equal(tx1, actualTx);
		}

		// Mirrored mempool with tx1 & existing tx1prevOut2.
		{
			IReadOnlySet<Transaction> transactions = mempoolMirror.GetSpenderTransactions(new[] { tx1prevOut2 });

			Transaction actualTx = Assert.Single(transactions);
			Assert.Equal(tx1, actualTx);
		}

		// Mirrored mempool with tx1 & existing tx1prevOut1 and tx1prevOut2.
		{
			IReadOnlySet<Transaction> transactions = mempoolMirror.GetSpenderTransactions(new[] { tx1prevOut1, tx1prevOut2 });

			Transaction actualTx = Assert.Single(transactions);
			Assert.Equal(tx1, actualTx);
		}
	}

	[Fact]
	public async Task CanCopyMempoolFromRpcAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			var mempoolInstance = services.Get<MempoolMirror>();
			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
			var spendAmount2 = new Money(0.0004m, MoneyUnit.BTC);
			await rpc.GenerateAsync(101);

			var txid = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			var txid2 = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount2);

			while ((await rpc.GetRawMempoolAsync()).Length != 2)
			{
				await Task.Delay(50);
			}

			await services.StartAllAsync();
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Equal(2, localMempoolHashes.Count);
			Assert.Contains(txid, localMempoolHashes);
			Assert.Contains(txid2, localMempoolHashes);
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
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(7), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			await services.StartAllAsync();

			await rpc.GenerateAsync(101);

			var txid = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), new Money(0.0004m, MoneyUnit.BTC));

			while (!(await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}

			var mempoolInstance = services.Get<MempoolMirror>();
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Single(localMempoolHashes);
			Assert.Contains(txid, localMempoolHashes);
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
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			using var k1 = new Key();
			var blockIds = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockIds[0]);
			var coinBaseTx = block.Transactions[0];

			var tx = Transaction.Create(network);
			tx.Inputs.Add(coinBaseTx, 0);
			tx.Outputs.Add(Money.Coins(49.9999m), BitcoinFactory.CreateBitcoinAddress(network));
			tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
			var valid = tx.Check();

			await rpc.GenerateAsync(101);

			for (int i = 0; i < 5; i++)
			{
				await rpc.SendRawTransactionAsync(tx);
			}

			while (!(await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Single(localMempoolHashes);
			Assert.Contains(tx.GetHash(), localMempoolHashes);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task CanHandleManyTxsAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			using var k1 = new Key();
			var blockIds = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockIds[0]);
			var coinBaseTx = block.Transactions[0];

			await rpc.GenerateAsync(101);

			for (int i = 0; i < 5; i++)
			{
				await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), new Money(0.0004m, MoneyUnit.BTC));
			}

			while ((await rpc.GetRawMempoolAsync()).Length != 5)
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Equal(5, localMempoolHashes.Count);
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
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);

			await rpc.GenerateAsync(101);

			var rpcMempoolBeforeSend = await rpc.GetRawMempoolAsync();
			var localMempoolBeforeSend = mempoolInstance.GetMempoolHashes();
			Assert.Equal(rpcMempoolBeforeSend.Length, localMempoolBeforeSend.Count);

			await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			while (!(await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolAfterSend = mempoolInstance.GetMempoolHashes();
			Assert.Equal(1, localMempoolAfterSend.Count);
			Assert.Single(localMempoolAfterSend);

			await rpc.GenerateAsync(1);
			while ((await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolAfterBlockMined = mempoolInstance.GetMempoolHashes();
			Assert.Empty(localMempoolAfterBlockMined);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync();
		}
	}
}
