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

	/// <summary>Mapping of transaction hashes to transactions.</summary>
	/// <remarks>Guarded by <see cref="MempoolLock"/>.</remarks>
	private Dictionary<uint256, Transaction> Mempool { get; } = new();

	/// <summary>Mapping of UTXOs to the corresponding spending transactions.</summary>
	/// <remarks>Guarded by <see cref="MempoolLock"/>.</remarks>
	private Dictionary<OutPoint, Transaction> PrevOutsIndex { get; } = new();

	/// <summary>Guards <see cref="Mempool"/> and <see cref="PrevOutsIndex"/>.</summary>
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
		var added = 0;
		lock (MempoolLock)
		{
			// Loop through the newly received transactions expect those that are already present in our mempool snapshot.
			foreach (Transaction tx in txs.Where(x => !Mempool.ContainsKey(x.GetHash())))
			{
				// Evict double spends.
				foreach (TxIn txInput in tx.Inputs)
				{
					// tx spends a UTXO which some *other* transaction in the mempool *already* spends.
					if (PrevOutsIndex.TryGetValue(txInput.PrevOut, out Transaction? mempoolTx))
					{
						// Remove the old transaction from our mempool snapshot.
						RemoveTransactionLocked(mempoolTx.GetHash());
					}
				}

				AddTransactionLocked(tx);
				added++;
			}
		}
		return added;
	}

	private void AddTransactionLocked(Transaction tx)
	{
		Mempool.Add(tx.GetHash(), tx);

		foreach (TxIn txInput in tx.Inputs)
		{
			PrevOutsIndex.Add(txInput.PrevOut, tx);
		}
	}

	private void RemoveTransactionLocked(uint256 txHash)
	{
		if (Mempool.Remove(txHash, out Transaction? transaction))
		{
			// Remove all UTXOs of the removed transaction from our helper index.
			foreach (TxIn txInput in transaction.Inputs)
			{
				_ = PrevOutsIndex.Remove(txInput.PrevOut);
			}
		}
	}

	private async Task<int> MirrorMempoolAsync(CancellationToken cancel)
	{
		var mempoolHashes = await Rpc.GetRawMempoolAsync(cancel).ConfigureAwait(false);

		IEnumerable<uint256> missingHashes;
		lock (MempoolLock)
		{
			var mempoolKeys = Mempool.Keys.ToImmutableArray();
			missingHashes = mempoolHashes.Except(mempoolKeys);

			foreach (var txid in mempoolKeys.Except(mempoolHashes).ToHashSet())
			{
				RemoveTransactionLocked(txid);
			}
		}

		IEnumerable<Transaction> missingTxs = await Rpc.GetRawTransactionsAsync(missingHashes, cancel).ConfigureAwait(false);
		var added = AddTransactions(missingTxs);
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
