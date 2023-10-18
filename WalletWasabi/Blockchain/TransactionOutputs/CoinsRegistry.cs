using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsRegistry : ICoinsView
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private bool InvalidateSnapshot { get; set; }

	private object Lock { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; } = new();

	/// <summary>Maps each outpoint to transactions (i.e. TxIds) that exist thanks to the outpoint. The values are also stored as keys in <see cref="CoinsByTransactionId"/>.</summary>
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, uint256> TxIdsByPrevOuts { get; } = new();

	/// <summary>Maps each TXID to smart coins (i.e. UTXOs).</summary>
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<uint256, IndexedCoinsWithAmount> CoinsByTransactionId { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<HdPubKey, HashSet<SmartCoin>> CoinsByPubKeys { get; } = new();

	private CoinsView AsCoinsViewNoLock()
	{
		UpdateSnapshotsNoLock();
		return new CoinsView(LatestCoinsSnapshot);
	}

	private CoinsView AsSpentCoinsViewNoLock()
	{
		UpdateSnapshotsNoLock();
		return new CoinsView(LatestSpentCoinsSnapshot);
	}

	private CoinsView AsCoinsView()
	{
		lock (Lock)
		{
			return AsCoinsViewNoLock();
		}
	}

	private CoinsView AsSpentCoinsView()
	{
		lock (Lock)
		{
			return AsSpentCoinsViewNoLock();
		}
	}

	private void UpdateSnapshotsNoLock()
	{
		if (!InvalidateSnapshot)
		{
			return;
		}

		var newCoinsSnapshot = new HashSet<SmartCoin>();
		var newSpentCoinsSnapshot = new HashSet<SmartCoin>();
		foreach (var coinByOutpoint in CoinsByTransactionId.Values.SelectMany(x => x.IndexedCoins))
		{
			if (coinByOutpoint.Value.SpenderTransaction is null)
			{
				newCoinsSnapshot.Add(coinByOutpoint.Value);
			}
			else
			{
				newSpentCoinsSnapshot.Add(coinByOutpoint.Value);
			}
		}

		LatestCoinsSnapshot = newCoinsSnapshot;
		LatestSpentCoinsSnapshot = newSpentCoinsSnapshot;
	}

	public bool TryAdd(SmartCoin coin)
	{
		lock (Lock)
		{
			return TryAddNoLock(coin);
		}
	}

	private bool TryAddNoLock(SmartCoin coin)
	{
		if (!CoinsByPubKeys.TryGetValue(coin.HdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
		{
			coinsOfPubKey = new();
			CoinsByPubKeys.Add(coin.HdPubKey, coinsOfPubKey);
		}
		coinsOfPubKey.Add(coin);

		if (!CoinsByTransactionId.TryGetValue(coin.TransactionId, out IndexedCoinsWithAmount? indexedCoinsWithAmount))
		{
			var dictionary = new Dictionary<uint, SmartCoin>();
			indexedCoinsWithAmount = new(dictionary);
			CoinsByTransactionId.Add(coin.TransactionId, indexedCoinsWithAmount);

			// Each prevOut of the transaction contributes to the existence of coins.
			// This only has to be added one per transaction.
			foreach (TxIn input in coin.Transaction.Transaction.Inputs)
			{
				TxIdsByPrevOuts[input.PrevOut] = coin.TransactionId;
			}
		}

		var added = indexedCoinsWithAmount.IndexedCoins.TryAdd(coin.Outpoint.N, coin);

		InvalidateSnapshot = InvalidateSnapshot || added;
		UpdateTransactionAmountNoLock(coin.TransactionId, coin.Amount);
		return added;
	}

	public void Spend(SmartCoin spentCoin, SmartTransaction tx)
	{
		tx.TryAddWalletInput(spentCoin);
		spentCoin.SpenderTransaction = tx;

		lock (Lock)
		{
			InvalidateSnapshot = true;
			UpdateTransactionAmountNoLock(tx.GetHash(), Money.Zero - spentCoin.Amount);
		}
	}

	private void UpdateTransactionAmountNoLock(uint256 txId, Money diff)
	{
		if(!CoinsByTransactionId.TryGetValue(txId, out var indexedCoinsWithAmount))
		{
			throw new InvalidOperationException($"Amount can't be updated for tx {txId} as it's not in CoinsByTransactionId cache");
		}

		indexedCoinsWithAmount.Amount += diff;
	}

	public void SwitchToUnconfirmFromBlock(Height blockHeight)
	{
		lock (Lock)
		{
			foreach (var coin in AsCoinsViewNoLock().AtBlockHeight(blockHeight))
			{
				var descendantCoins = DescendantOfNoLock(coin, includeSelf: true);
				foreach (var toSwitch in descendantCoins)
				{
					toSwitch.Height = Height.Mempool;
				}
			}
		}
	}

	/// <returns><c>true</c> if the transaction given by the txid contains at least one of our coins (either spent or unspent), <c>false</c> otherwise.</returns>
	public bool IsKnown(uint256 txid)
	{
		lock (Lock)
		{
			return CoinsByTransactionId.ContainsKey(txid);
		}
	}

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		coin = null;
		lock (Lock)
		{
			if(!CoinsByTransactionId.TryGetValue(outpoint.Hash, out var coinWithAmount))
			{
				return false;
			}

			return coinWithAmount.IndexedCoins.TryGetValue(outpoint.N, out coin);
		}
	}

	public bool TryGetCoinsByInputPrevOut(OutPoint prevOut, [NotNullWhen(true)] out HashSet<SmartCoin>? coins)
	{
		lock (Lock)
		{
			return TryGetCoinsByInputPrevOutNoLock(prevOut, out coins);
		}
	}

	private bool TryGetCoinsByInputPrevOutNoLock(OutPoint prevOut, [NotNullWhen(true)] out HashSet<SmartCoin>? coins)
	{
		coins = null;
		if (!TxIdsByPrevOuts.TryGetValue(prevOut, out var txId))
		{
			return false;
		}

		if(!CoinsByTransactionId.TryGetValue(txId, out var indexedCoinsWithAmount))
		{
			return false;
		}

		coins = indexedCoinsWithAmount.IndexedCoins.Values.ToHashSet();
		return true;
	}

	internal (ICoinsView toRemove, ICoinsView toAdd) Undo(uint256 txId)
	{
		lock (Lock)
		{
			var allCoins = AsAllCoinsViewNoLock();
			var toRemove = new HashSet<SmartCoin>();
			var toAdd = new HashSet<SmartCoin>();

			// Find all TransactionsIds to remove
			HashSet<uint256> txIdsToRemove = new();
			foreach (SmartCoin createdCoin in allCoins.CreatedBy(txId))
			{
				foreach(var descendantOrSelf in DescendantOf(createdCoin, true))
				{
					txIdsToRemove.Add(descendantOrSelf.TransactionId);
				}
			}

			foreach (var txIdToRemove in txIdsToRemove)
			{
				if (!CoinsByTransactionId.Remove(txIdToRemove, out var coinsToRemoveWithAmount))
				{
					throw new InvalidOperationException(
						$"Failed to remove '{txIdToRemove}' from {nameof(CoinsByTransactionId)}.");
				}

				foreach (var coinToRemove in coinsToRemoveWithAmount.IndexedCoins.Values)
				{
					if (toRemove.Add(coinToRemove))
					{
						continue;
					}

					if (!CoinsByPubKeys.TryGetValue(coinToRemove.HdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
					{
						continue;
					}

					coinsOfPubKey.Remove(coinToRemove);

					if (coinsOfPubKey.Count == 0)
					{
						CoinsByPubKeys.Remove(coinToRemove.HdPubKey);
					}
				}

				// Remove the prevOut of the inputs of the transaction from TxIdsByInputsPrevOut cache.
				// This cache can be really big and it's better to avoid .ToList().
				var keysToRemove = new HashSet<OutPoint>();
				foreach (var removedTxIdByInputPrevOut in TxIdsByPrevOuts.Where(x => x.Value.Equals(txIdToRemove)))
				{
					keysToRemove.Add(removedTxIdByInputPrevOut.Key);
				}

				foreach (var keyToRemove in keysToRemove)
				{
					TxIdsByPrevOuts.Remove(keyToRemove);
				}

				// destroyed (spent) coins are now (unspent)
				foreach (SmartCoin destroyedCoin in allCoins.SpentBy(txIdToRemove))
				{
					destroyedCoin.SpenderTransaction = null;
					toAdd.Add(destroyedCoin);
				}
			}

			InvalidateSnapshot = InvalidateSnapshot || toRemove.Any() || toAdd.Any();
			return (new CoinsView(toRemove), new CoinsView(toAdd));
		}
	}

	public IReadOnlyList<SmartCoin> GetMyInputs(SmartTransaction transaction)
	{
		var myInputs = new List<SmartCoin>();

		lock (Lock)
		{
			foreach (TxIn input in transaction.Transaction.Inputs)
			{
				if (!CoinsByTransactionId.TryGetValue(input.PrevOut.Hash, out IndexedCoinsWithAmount? indexedCoinsWithAmount))
				{
					continue;
				}

				if (indexedCoinsWithAmount.IndexedCoins.TryGetValue(input.PrevOut.N, out var coin))
				{
					myInputs.Add(coin);
				}
			}
		}

		return myInputs;
	}

	/// <returns><c>true</c> if the coin registry contains at least one unspent coin with <paramref name="hdPubKey"/>, <c>false</c> otherwise.</returns>
	public bool HasUnspentCoin(HdPubKey hdPubKey)
	{
		lock (Lock)
		{
			if (CoinsByPubKeys.TryGetValue(hdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
			{
				return coinsOfPubKey.Any(coin => !coin.IsSpent());
			}

			return false;
		}
	}

	public bool IsUsed(HdPubKey hdPubKey)
	{
		lock (Lock)
		{
			return CoinsByPubKeys.TryGetValue(hdPubKey, out _);
		}
	}

	/// <summary>Gets transaction amount representing change in wallet balance for the wallet the transaction belongs to.</summary>
	/// <returns>The same value as <see cref="TransactionSummary.Amount"/>.</returns>
	public bool TryGetTxAmount(uint256 txid, [NotNullWhen(true)] out Money? amount)
	{
		amount = null;
		lock (Lock)
		{
			if (!CoinsByTransactionId.TryGetValue(txid, out var indexedCoinsWithAmount))
			{
				return false;
			}

			amount = indexedCoinsWithAmount.Amount;
			return true;
		}
	}

	/// <summary>Gets total balance as a sum of unspent coins.</summary>
	public Money GetTotalBalance()
	{
		lock (Lock)
		{
			// Amount can be hold as a variable that is updated every time to avoid summing it.
			return CoinsByTransactionId.Values.Sum(x => x.Amount);
		}
	}

	public ICoinsView AsAllCoinsView()
	{
		lock (Lock)
		{
			return new CoinsView(AsAllCoinsViewNoLock().ToList());
		}
	}

	private ICoinsView AsAllCoinsViewNoLock() => new CoinsView(AsCoinsViewNoLock().Concat(AsSpentCoinsViewNoLock()).ToList());

	public ICoinsView AtBlockHeight(Height height) => AsCoinsView().AtBlockHeight(height);

	public ICoinsView Available() => AsCoinsView().Available();

	public ICoinsView CoinJoinInProcess() => AsCoinsView().CoinJoinInProcess();

	public ICoinsView Confirmed() => AsCoinsView().Confirmed();

	/// <summary>Gets descendant coins of the given coin - i.e. all coins that spent the input coin, all coins that spent those coins, etc.</summary>
	public ICoinsView DescendantOf(SmartCoin coin, bool includeSelf)
	{
		lock (Lock)
		{
			return new CoinsView(DescendantOfNoLock(coin, includeSelf));
		}
	}

	/// <remarks>Callers must acquire <see cref="Lock"/> before calling this method.</remarks>
	private ImmutableArray<SmartCoin> DescendantOfNoLock(SmartCoin coin, bool includeSelf)
	{
		ICoinsView allCoins = AsAllCoinsViewNoLock();

		IEnumerable<SmartCoin> Generator(SmartCoin parentCoin, bool addSelf)
		{
			IEnumerable<SmartCoin> childrenOf = parentCoin.SpenderTransaction is not null
				? allCoins.Where(x => x.TransactionId == parentCoin.SpenderTransaction.GetHash()) // Inefficient.
				: Array.Empty<SmartCoin>();

			foreach (var child in childrenOf)
			{
				foreach (var childDescendant in Generator(child, addSelf: false))
				{
					yield return childDescendant;
				}

				yield return child;
			}

			// Return self.
			if (addSelf)
			{
				yield return parentCoin;
			}
		}

		return Generator(coin, addSelf: includeSelf).ToImmutableArray();
	}

	public ICoinsView FilterBy(Func<SmartCoin, bool> expression) => AsCoinsView().FilterBy(expression);

	public IEnumerator<SmartCoin> GetEnumerator() => AsCoinsView().GetEnumerator();

	public ICoinsView CreatedBy(uint256 txid) => AsCoinsView().CreatedBy(txid);

	public ICoinsView SpentBy(uint256 txid) => AsSpentCoinsView().SpentBy(txid);

	public SmartCoin[] ToArray() => AsCoinsView().ToArray();

	public Money TotalAmount() => AsCoinsView().TotalAmount();

	public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

	public ICoinsView Unspent() => AsCoinsView().Unspent();

	IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();

	private record IndexedCoinsWithAmount(Dictionary<uint, SmartCoin> IndexedCoins)
	{
		public Money Amount { get; set; } = 0;
	}
}
