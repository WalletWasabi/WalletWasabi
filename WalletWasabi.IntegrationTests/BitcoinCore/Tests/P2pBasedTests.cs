using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.IntegrationTests.Infrastructure;
using Xunit;

namespace WalletWasabi.IntegrationTests.BitcoinCore.Tests;

public class P2pBasedTests
{
	[Fact]
	public async Task MempoolNotifiesAsync()
	{
		string dir = await GetEmptyWorkDirAsync();

		var coreNode = await TestNodeBuilder.CreateAsync();
		using var node = await coreNode.CreateNewP2pNodeAsync();

		try
		{
			var network = coreNode.Network;
			var rpc = coreNode.RpcClient;

			var walletName = "wallet";
			await rpc.CreateWalletAsync(walletName);

			var filterHeaderChain = new FilterHeaderChain();
			using var transactionStore = new AllTransactionStore(Path.Combine(dir, "transactionStore"), network);
			await transactionStore.InitializeAsync(CancellationToken.None);

			using var filterStore = new FilterStore(Path.Combine(dir, "indexStore"), network, filterHeaderChain, TestNodeBuilder.EventBus);
			await filterStore.InitializeAsync(new Height.ChainHeight(0u), CancellationToken.None);

			var mempoolService = coreNode.MempoolService;

			await rpc.GenerateAsync(blockCount: 101);

			node.Behaviors.Add(new P2pBehavior(mempoolService));
			await node.VersionHandshakeAsync();

			// TODO: It's unclear why it helps the test to pass.
			await Task.Delay(3000);

			using var k = new Key();
			var address = k.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);

			// Number of transactions to send.
			const int TransactionsCount = 3;

			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
			var awaiter = TestNodeBuilder.EventBus.WaitForAsync<NewTransactionInMempool, SmartTransaction>(TransactionsCount, e => e.Transaction, cts.Token);

			var txHashesTasks = new Task<uint256>[TransactionsCount];

			// Add to the batch 3 RPC commands: Send 1 coin to the same address.
			for (int i = 0; i < TransactionsCount; i++)
			{
				Task<uint256> txidTask = rpc.SendToAddressAsync(address, Money.Coins(1));
				txHashesTasks[i] = txidTask;
			}

			var txHashes = await Task.WhenAll(txHashesTasks);

			// Wait until the mempool service receives all the sent transactions.
			var mempoolSmartTxs = await awaiter;

			// Check that all the received transaction hashes are in the set of sent transaction hashes.
			foreach (var tx in mempoolSmartTxs)
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

	private static async Task<string> GetEmptyWorkDirAsync()
	{
		var dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "IntegrationTests"));
		var workDir = Path.Combine(dataDir, nameof(P2pBasedTests), nameof(MempoolNotifiesAsync));

		if (Directory.Exists(workDir))
		{
			await IoHelpers.TryDeleteDirectoryAsync(workDir).ConfigureAwait(false);
		}

		Directory.CreateDirectory(workDir);
		return workDir;
	}
}

public static class EventBusExtensions
{
	public static Task<TResult[]> WaitForAsync<TEvent, TResult>(this EventBus eventBus, int count, Func<TEvent, TResult> conv, CancellationToken cancellationToken) where TEvent : notnull
	{
		var events = new ConcurrentQueue<TEvent>();
		var completion = new TaskCompletionSource<TResult[]>();
		var subscription = eventBus.Subscribe<TEvent>(e =>
		{
			events.Enqueue(e);

			if (events.Count == count)
			{
				completion.SetResult(events.Select(conv).ToArray());
			}
		});

		return completion.Task
			.WithCancellation(cancellationToken)
			.ContinueWith(r =>
				{
					subscription.Dispose();
					return r.Result;
				},
				cancellationToken);
	}
}
