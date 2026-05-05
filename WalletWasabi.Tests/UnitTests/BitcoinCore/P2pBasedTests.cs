using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.BitcoinCore;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class P2pBasedTests
{
	[Fact]
	public async Task MempoolNotifiesAsync()
	{
		Console.WriteLine($"==== Run #1 ====");
		CoreNode coreNode = await TestNodeBuilder.CreateAsync();

		using var node = await coreNode.CreateNewP2pNodeAsync();

		try
		{
			Console.WriteLine($"MempoolNotifiesAsync - 1st");
			string dir = Common.GetWorkDir();
			var network = coreNode.Network;
			var rpc = coreNode.RpcClient;

			var walletName = "wallet";
			await rpc.CreateWalletAsync(walletName);

			Console.WriteLine($"MempoolNotifiesAsync - 2nd");
			SmartHeaderChain smartHeaderChain = new();
			await using AllTransactionStore transactionStore = new(Path.Combine(dir, "transactionStore"), network);
			await transactionStore.InitializeAsync(CancellationToken.None);

			Console.WriteLine($"MempoolNotifiesAsync - 3rd");
			await using FilterStore filterStore = new(Path.Combine(dir, "indexStore"), network, smartHeaderChain, TestNodeBuilder.EventBus);
			await filterStore.InitializeAsync(new Height.ChainHeight(0u), CancellationToken.None);

			MempoolService mempoolService = coreNode.MempoolService;

			Console.WriteLine($"MempoolNotifiesAsync - 4th");
			await rpc.GenerateAsync(blockCount: 101);

			node.Behaviors.Add(new P2pBehavior(mempoolService));
			node.VersionHandshake();

			using Key k = new();
			var address = k.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);

			// Number of transactions to send.
			const int TransactionsCount = 3;

			Console.WriteLine($"MempoolNotifiesAsync - 5th");
			var eventBus = TestNodeBuilder.EventBus;
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
			var awaiter = eventBus.WaitForAsync<NewTransactionInMempool, SmartTransaction>(TransactionsCount, e => e.Transaction, cts.Token);

			Console.WriteLine($"MempoolNotifiesAsync - 6th");
			Task<uint256>[] txHashesTasks = new Task<uint256>[TransactionsCount];

			// Add to the batch 3 RPC commands: Send 1 coin to the same address.
			for (int i = 0; i < TransactionsCount; i++)
			{
				Console.WriteLine($"MempoolNotifiesAsync - 7th - {i}");
				Task<uint256> txidTask = rpc.SendToAddressAsync(address, Money.Coins(1));
				txHashesTasks[i] = txidTask;
			}

			Console.WriteLine($"MempoolNotifiesAsync - 8th");
			uint256[] txHashes = await Task.WhenAll(txHashesTasks);

			Console.WriteLine($"MempoolNotifiesAsync - 9th");
			// Wait until the mempool service receives all the sent transactions.
			IEnumerable<SmartTransaction> mempoolSmartTxs = await awaiter;

			Console.WriteLine($"MempoolNotifiesAsync - 10th");
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

public static class EventBusExtensions
{
	public static Task<TResult[]> WaitForAsync<TEvent, TResult>(this EventBus eventBus, int count, Func<TEvent, TResult> conv, CancellationToken cancellationToken) where TEvent : notnull
	{
		var events = new List<TEvent>();
		var completion = new TaskCompletionSource<TResult[]>();
		var subscription = eventBus.Subscribe<TEvent>(e =>
		{
			events.Add(e);
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

