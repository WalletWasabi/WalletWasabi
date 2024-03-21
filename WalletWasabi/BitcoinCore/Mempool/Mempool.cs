using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.BitcoinCore.Mempool;

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
	/// <exception cref="InvalidOperationException">If there is an internal issue.</exception>
	public void AddTransaction(Transaction tx)
	{
		// Evict double spends.
		foreach (TxIn txInput in tx.Inputs)
		{
			// tx spends a UTXO which some *other* transaction in the mempool *already* spends.
			if (PrevOutsIndex.TryGetValue(txInput.PrevOut, out Transaction? mempoolTx))
			{
				// Remove the old transaction from our mempool snapshot.
				if (!TryRemoveTransaction(mempoolTx.GetHash()))
				{
					throw new InvalidOperationException($"Failed to remove '{mempoolTx.GetHash()}' transaction.");
				}
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
	public bool TryRemoveTransaction(uint256 txHash)
	{
		if (TransactionIndex.Remove(txHash, out Transaction? transaction))
		{
			// Remove all UTXOs of the removed transaction from our helper index.
			foreach (TxIn txInput in transaction.Inputs)
			{
				PrevOutsIndex.Remove(txInput.PrevOut);
			}

			return true;
		}

		return false;
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
		HashSet<OutPoint> txOutsSet = txOuts.ToHashSet();

		return txOutsSet.Where(prevOut => PrevOutsIndex.ContainsKey(prevOut))
			.Select(prevOut => PrevOutsIndex[prevOut])
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
