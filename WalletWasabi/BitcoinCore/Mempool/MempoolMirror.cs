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

	public IRPCClient Rpc { get; }
	public P2pNode Node { get; }
	private Dictionary<uint256, Transaction> Mempool { get; } = new();
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

	private int AddTransactions(params Transaction[] txs) => AddTransactions(txs as IEnumerable<Transaction>);

	private int AddTransactions(IEnumerable<Transaction> txs)
	{
		var added = 0;
		lock (MempoolLock)
		{
			foreach (var tx in txs.Where(x => !Mempool.ContainsKey(x.GetHash())))
			{
				// Evict double spents.
				EvictSpendersNoLock(tx.Inputs.Select(x => x.PrevOut));

				Mempool.Add(tx.GetHash(), tx);
				added++;
			}
		}
		return added;
	}

	private void EvictSpendersNoLock(IEnumerable<OutPoint> txOuts)
	{
		HashSet<uint256> doubleSpents = new();
		foreach (var input in txOuts)
		{
			foreach (var mempoolTx in Mempool)
			{
				if (mempoolTx.Value.Inputs.Select(x => x.PrevOut).Contains(input))
				{
					doubleSpents.Add(mempoolTx.Key);
				}
			}
		}

		foreach (var txid in doubleSpents)
		{
			Mempool.Remove(txid);
		}
	}

	private async Task<int> MirrorMempoolAsync(CancellationToken cancel)
	{
		var mempoolHashes = await Rpc.GetRawMempoolAsync(cancel).ConfigureAwait(false);

		IEnumerable<uint256> missing;
		lock (MempoolLock)
		{
			var mempoolKeys = Mempool.Keys.ToImmutableArray();
			missing = mempoolHashes.Except(mempoolKeys);

			foreach (var txid in mempoolKeys.Except(mempoolHashes).ToHashSet())
			{
				Mempool.Remove(txid);
			}
		}

		var added = AddTransactions(await Rpc.GetRawTransactionsAsync(missing, cancel).ConfigureAwait(false));
		return added;
	}

	public IEnumerable<Transaction> GetSpenderTransactions(IEnumerable<OutPoint> txOuts)
	{
		Dictionary<uint256, Transaction> spenders = new();
		lock (MempoolLock)
		{
			foreach (var input in txOuts)
			{
				foreach (var mempoolTx in Mempool)
				{
					if (mempoolTx.Value.Inputs.Select(x => x.PrevOut).Contains(input))
					{
						spenders.Add(mempoolTx.Key, mempoolTx.Value);
					}
				}
			}
		}

		return spenders.Values;
	}

	public ISet<uint256> GetMempoolHashes()
	{
		lock (MempoolLock)
		{
			return Mempool.Keys.ToHashSet();
		}
	}
}
