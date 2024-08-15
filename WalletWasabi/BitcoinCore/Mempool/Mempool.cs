using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.BitcoinCore.Mempool;

/// <remarks>This class is not thread-safe.</remarks>
public class Mempool
{
	/// <summary>Mapping of transaction hashes to the corresponding transactions.</summary>
	private Dictionary<uint256, byte> TransactionIndex { get; init; } = new();

	/// <summary>
	/// Checks whether a transaction with the given hash is in the mempool.
	/// </summary>
	public bool ContainsTransaction(uint256 txHash)
	{
		return TransactionIndex.ContainsKey(txHash);
	}

	/// <summary>
	/// Adds a new transaction to the mempool.
	/// </summary>
	/// <remarks>
	/// Double spends are not evicted, but they will be invalidated at every call of GetRawMempoolAsync
	/// </remarks>
	public bool AddTransaction(uint256 txHash)
		=> TransactionIndex.TryAdd(txHash, new byte());

	/// <summary>
	/// Adds the given transactions to the mempool.
	/// <para>If the transaction is already in the mempool, it is not added.</para>
	/// </summary>
	/// <returns>Number of added transactions.</returns>
	public int AddMissingTransactions(IEnumerable<uint256> txsHash)
	{
		int added = 0;
		foreach (uint256 txHash in txsHash.Where(x => !ContainsTransaction(x)))
		{
			TransactionIndex.TryAdd(txHash, new byte());
			added++;
		}

		return added;
	}

	/// <summary>
	/// Removes the transaction with the given transaction hash from the mempool.
	/// </summary>
	public bool RemoveTransaction(uint256 txHashToRemove)
		=> TransactionIndex.Remove(txHashToRemove, out _);

	/// <summary>
	/// Gets a copy of all transaction hashes in the mempool.
	/// </summary>
	public ISet<uint256> GetMempoolTxids()
	{
		return TransactionIndex.Keys.ToHashSet();
	}
}
