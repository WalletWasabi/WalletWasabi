using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.BitcoinCore;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class P2pBasedTests
{
	// [Fact]  FIXME: this test never fails locally while almost always fail in the CI server
	public async Task MempoolNotifiesAsync()
	{
		CoreNode coreNode = await TestNodeBuilder.CreateAsync();

		using var node = await coreNode.CreateNewP2pNodeAsync();

		try
		{
			string dir = Common.GetWorkDir();
			var network = coreNode.Network;
			var rpc = coreNode.RpcClient;

			var walletName = "wallet";
			await rpc.CreateWalletAsync(walletName);

			SmartHeaderChain smartHeaderChain = new();
			await using FilterStore filterStore = new(Path.Combine(dir, "indexStore"), network, smartHeaderChain);
			await using AllTransactionStore transactionStore = new(Path.Combine(dir, "transactionStore"), network);
			MempoolService mempoolService = coreNode.MempoolService;
			FileSystemBlockRepository blocks = new(Path.Combine(dir, "blocks"), network);

			// Construct BitcoinStore.
			BitcoinStore bitcoinStore = new(filterStore, transactionStore, mempoolService, smartHeaderChain, blocks);
			await bitcoinStore.InitializeAsync();

			await rpc.GenerateAsync(blockCount: 101);

			node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());
			node.VersionHandshake();

			using Key k = new();
			var address = k.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);

			// Number of transactions to send.
			const int TransactionsCount = 3;

			EventsAwaiter<SmartTransaction> eventAwaiter = new(
				subscribe: h => mempoolService.TransactionReceived += h,
				unsubscribe: h => mempoolService.TransactionReceived -= h,
				count: TransactionsCount);

			Task<uint256>[] txHashesTasks = new Task<uint256>[TransactionsCount];

			// Add to the batch 10 RPC commands: Send 1 coin to the same address.
			for (int i = 0; i < TransactionsCount; i++)
			{
				Task<uint256> txidTask = rpc.SendToAddressAsync(address, Money.Coins(1));
				txHashesTasks[i] = txidTask;
			}

			uint256[] txHashes = await Task.WhenAll(txHashesTasks);

			// Wait until the mempool service receives all the sent transactions.
			IEnumerable<SmartTransaction> mempoolSmartTxs = await eventAwaiter.WaitAsync(TimeSpan.FromMinutes(4));

			// Check that all the received transaction hashes are in the set of sent transaction hashes.
			foreach (SmartTransaction tx in mempoolSmartTxs)
			{
				Assert.Contains(tx.GetHash(), txHashes);
			}
		}
		finally
		{
			node.DisconnectAsync();
			await coreNode.TryStopAsync();
		}
	}
}
