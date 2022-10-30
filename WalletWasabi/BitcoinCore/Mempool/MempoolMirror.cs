using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Mempool;

public class MempoolMirror : PeriodicRunner
{
	/// <param name="period">How often to mirror the mempool.</param>
	public MempoolMirror(TimeSpan period, IRPCClient rpc, P2pNode node) : base(period)
	{
		Rpc = rpc;
		Node = node;
	}

	private IRPCClient Rpc { get; }
	private P2pNode Node { get; }
	private Dictionary<uint256, Transaction> Mempool { get; set; } = new();
	private object MempoolLock { get; } = new();

	public override Task StartAsync(CancellationToken cancellationToken)
	{
		Node.MempoolService.TransactionReceived += MempoolService_TransactionReceived;
		return base.StartAsync(cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var sw = Stopwatch.StartNew();
		int added = await MirrorMempoolAsync(stoppingToken).ConfigureAwait(false);
		Logger.LogInfo($"{added} transactions were copied from the full node to the in-memory mempool within {sw.Elapsed.TotalSeconds} seconds.");

		await base.ExecuteAsync(stoppingToken).ConfigureAwait(false);
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		await MirrorMempoolAsync(cancel).ConfigureAwait(false);
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		Node.MempoolService.TransactionReceived -= MempoolService_TransactionReceived;
		return base.StopAsync(cancellationToken);
	}

	private void MempoolService_TransactionReceived(object? sender, SmartTransaction stx)
	{
		AddTransactions(stx.Transaction);
	}

	internal int AddTransactions(params Transaction[] txs) => AddTransactions(txs as IEnumerable<Transaction>);

	private int AddTransactions(IEnumerable<Transaction> txs)
	{
		lock (MempoolLock)
		{
			return AddTransactionsToMempool(Mempool, txs);
		}
	}

	private static int AddTransactionsToMempool(Dictionary<uint256, Transaction> mempool, IEnumerable<Transaction> txs)
	{
		int added = 0;
		foreach (var tx in txs.Where(x => !mempool.ContainsKey(x.GetHash())))
		{
			// Evict double spends.
			EvictSpendersFromMempool(mempool, tx.Inputs.Select(x => x.PrevOut));

			mempool.Add(tx.GetHash(), tx);
			added++;
		}
		return added;
	}

	private static void EvictSpendersFromMempool(Dictionary<uint256, Transaction> mempool, IEnumerable<OutPoint> txOuts)
	{
		HashSet<uint256> doubleSpents = new();
		foreach (var input in txOuts)
		{
			foreach (var mempoolTx in mempool)
			{
				if (mempoolTx.Value.Inputs.Select(x => x.PrevOut).Contains(input))
				{
					doubleSpents.Add(mempoolTx.Key);
				}
			}
		}

		foreach (var txid in doubleSpents)
		{
			mempool.Remove(txid);
		}
	}

	private async Task<int> MirrorMempoolAsync(CancellationToken cancel)
	{
		var mempoolHashes = await Rpc.GetRawMempoolAsync(cancel).ConfigureAwait(false);

		Dictionary<uint256, Transaction> newMempool;

		IEnumerable<uint256> missingHashes;
		lock (MempoolLock)
		{
			// Copy mempool.
			newMempool = new(Mempool);

			ImmutableArray<uint256> mempoolKeys = newMempool.Keys.ToImmutableArray();
			missingHashes = mempoolHashes.Except(mempoolKeys);

			foreach (var txid in mempoolKeys.Except(mempoolHashes).ToHashSet())
			{
				newMempool.Remove(txid);
			}
		}

		IEnumerable<Transaction> missingTxs = await Rpc.GetRawTransactionsAsync(missingHashes, cancel).ConfigureAwait(false);

		int added = AddTransactionsToMempool(newMempool, missingTxs);

		lock (MempoolLock)
		{
			Mempool = newMempool;
		}

		return added;
	}

	public IReadOnlySet<Transaction> GetSpenderTransactions(IEnumerable<OutPoint> txOuts)
	{
		lock (MempoolLock)
		{
			var mempoolTxs = Mempool.Values;
			var txOutsSet = txOuts.ToHashSet();

			return mempoolTxs.SelectMany(tx => tx.Inputs.Select(i => (tx, i.PrevOut)))
				.Where(x => txOutsSet.Contains(x.PrevOut))
				.Select(x => x.tx)
				.ToHashSet();
		}
	}

	public ISet<uint256> GetMempoolHashes()
	{
		lock (MempoolLock)
		{
			return Mempool.Keys.ToHashSet();
		}
	}
}
