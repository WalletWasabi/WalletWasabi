using NBitcoin;
using System.Collections.Generic;
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
	private Mempool Mempool { get; set; } = new();
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

	internal int AddTransactions(params Transaction[] txs)
	{
		lock (MempoolLock)
		{
			return Mempool.AddMissingTransactions(txs);
		}
	}

	private async Task<int> MirrorMempoolAsync(CancellationToken cancel)
	{
		// Get all TXIDs in the up-to-date mempool.
		uint256[]? newTxids = await Rpc.GetRawMempoolAsync(cancel).ConfigureAwait(false);

		Mempool newMempool;

		// TXIDs in the new mempool, but not in the old mempool (what we currently have).
		IEnumerable<uint256> missingTxids;

		lock (MempoolLock)
		{
			newMempool = Mempool.Clone();

			ISet<uint256> oldTxids = Mempool.GetMempoolTxids();

			// Those TXIDs that are in the new mempool snapshot but not in the old one, are the ones
			// for which we want to download the corresponding transactions via RPC.
			missingTxids = newTxids.Except(oldTxids);

			// Remove those transactions that are not present in the new mempool snapshot.
			foreach (uint256 txid in oldTxids.Except(newTxids).ToHashSet())
			{
				newMempool.RemoveTransaction(txid);
			}
		}

		IEnumerable<Transaction> missingTxs = await Rpc.GetRawTransactionsAsync(missingTxids, cancel).ConfigureAwait(false);

		int added = newMempool.AddMissingTransactions(missingTxs);

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
			return Mempool.GetSpenderTransactions(txOuts);
		}
	}

	internal ISet<uint256> GetMempoolHashes()
	{
		lock (MempoolLock)
		{
			return Mempool.GetMempoolTxids();
		}
	}
}

/// <remarks>This class is not thread-safe.</remarks>
public class Mempool
{
	/// <summary>Mapping of transaction hashes to the corresponding transactions.</summary>
	private Dictionary<uint256, Transaction> TransactionIndex { get; init; } = new();

	/// <summary>Mapping of UTXOs to the corresponding spending transactions.</summary>
	private Dictionary<OutPoint, Transaction> PrevOutsIndex { get; init; } = new();

	/// <summary>
	/// Checks whether a transaction with the given hash is in the mempool.
	/// </summary>
	public bool ContainsTransaction(uint256 txHash)
	{
		return TransactionIndex.ContainsKey(txHash);
	}

	/// <summary>
	/// Adds a new transaction to the mempool.
	/// If <paramref name="tx"/> spends an <see cref="OutPoint"/> that is already spent by a different mempool transaction
	/// in the mempool, we remove the mempool transaction(s).
	/// </summary>
	/// <exception cref="ArgumentException">If the transaction is already in the mempool.</exception>
	public void AddTransaction(Transaction tx)
	{
		// Evict double spends.
		foreach (TxIn txInput in tx.Inputs)
		{
			// tx spends a UTXO which some *other* transaction in the mempool *already* spends.
			if (PrevOutsIndex.TryGetValue(txInput.PrevOut, out Transaction? mempoolTx))
			{
				// Remove the old transaction from our mempool snapshot.
				RemoveTransaction(mempoolTx.GetHash());
			}
		}

		TransactionIndex.Add(tx.GetHash(), tx);

		foreach (TxIn txInput in tx.Inputs)
		{
			PrevOutsIndex.Add(txInput.PrevOut, tx);
		}
	}

	/// <summary>
	/// Adds the given transactions to the mempool.
	/// <para>If the transaction is already in the mempool, it is not added.</para>
	/// </summary>
	/// <returns>Number of added transactions.</returns>
	public int AddMissingTransactions(IEnumerable<Transaction> txs)
	{
		int added = 0;
		foreach (Transaction tx in txs.Where(x => !ContainsTransaction(x.GetHash())))
		{
			AddTransaction(tx);
			added++;
		}

		return added;
	}

	/// <summary>
	/// Removes the transaction with the given transaction hash from the mempool.
	/// </summary>
	public void RemoveTransaction(uint256 txHash)
	{
		if (TransactionIndex.Remove(txHash, out Transaction? transaction))
		{
			// Remove all UTXOs of the removed transaction from our helper index.
			foreach (TxIn txInput in transaction.Inputs)
			{
				_ = PrevOutsIndex.Remove(txInput.PrevOut);
			}
		}
	}

	/// <summary>
	/// Gets a copy of all transaction hashes in the mempool.
	/// </summary>
	public ISet<uint256> GetMempoolTxids()
	{
		return TransactionIndex.Keys.ToHashSet();
	}

	/// <summary>
	/// Gets transactions from the mempool that spends <paramref name="txOuts"/>.
	/// </summary>
	public IReadOnlySet<Transaction> GetSpenderTransactions(IEnumerable<OutPoint> txOuts)
	{
		IEnumerable<Transaction> transactions = TransactionIndex.Values;
		HashSet<OutPoint> txOutsSet = txOuts.ToHashSet();

		return transactions.SelectMany(tx => tx.Inputs.Select(i => (tx, i.PrevOut)))
			.Where(x => txOutsSet.Contains(x.PrevOut))
			.Select(x => x.tx)
			.ToHashSet();
	}

	public Mempool Clone()
	{
		return new Mempool()
		{
			TransactionIndex = new Dictionary<uint256, Transaction>(TransactionIndex),
			PrevOutsIndex = new Dictionary<OutPoint, Transaction>(PrevOutsIndex)
		};
	}
}
