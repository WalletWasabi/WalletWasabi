using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsRegistry : ICoinsView
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> Coins { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<uint256> KnownTransactions { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, SmartCoin> OutpointCoinCache { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private bool InvalidateSnapshot { get; set; }

	private object Lock { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> SpentCoins { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, SmartCoin> SpentCoinsByOutPoint { get; } = new();

	private CoinsView AsCoinsViewNoLock()
	{
		if (InvalidateSnapshot)
		{
			LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
			LatestSpentCoinsSnapshot = SpentCoins.ToHashSet(); // Creates a clone
			InvalidateSnapshot = false;
		}

		return new CoinsView(LatestCoinsSnapshot);
	}

	private CoinsView AsSpentCoinsViewNoLock()
	{
		if (InvalidateSnapshot)
		{
			LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
			LatestSpentCoinsSnapshot = SpentCoins.ToHashSet(); // Creates a clone
			InvalidateSnapshot = false;
		}

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

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin) => AsCoinsView().TryGetByOutPoint(outpoint, out coin);

	public bool TryAdd(SmartCoin coin)
	{
		var added = false;
		lock (Lock)
		{
			if (!SpentCoins.Contains(coin))
			{
				added = Coins.Add(coin);
				if (added)
				{
					coin.RegisterToHdPubKey();

					KnownTransactions.Add(coin.TransactionId);
					OutpointCoinCache.AddOrReplace(coin.Outpoint, coin);

					InvalidateSnapshot = true;
				}
			}
		}

		return added;
	}

	private ICoinsView RemoveNoLock(SmartCoin coin)
	{
		var coinsToRemove = DescendantOfAndSelfNoLock(coin);
		foreach (var toRemove in coinsToRemove)
		{
			if (!Coins.Remove(toRemove))
			{
				if (SpentCoins.Remove(toRemove))
				{
					SpentCoinsByOutPoint.Remove(toRemove.Outpoint);
				}
			}

			toRemove.UnregisterFromHdPubKey();

			var removedCoinOutPoint = toRemove.Outpoint;
			OutpointCoinCache.Remove(removedCoinOutPoint);
		}

		InvalidateSnapshot = true;
		return coinsToRemove;
	}

	public void Spend(SmartCoin spentCoin, SmartTransaction tx)
	{
		tx.TryAddWalletInput(spentCoin);
		spentCoin.SpenderTransaction = tx;

		lock (Lock)
		{
			if (Coins.Remove(spentCoin))
			{
				InvalidateSnapshot = true;
				if (SpentCoins.Add(spentCoin))
				{
					SpentCoinsByOutPoint.Add(spentCoin.Outpoint, spentCoin);
				}
			}
		}
	}

	public void SwitchToUnconfirmFromBlock(Height blockHeight)
	{
		lock (Lock)
		{
			foreach (var coin in AsCoinsViewNoLock().AtBlockHeight(blockHeight))
			{
				var descendantCoins = DescendantOfAndSelf(coin);
				foreach (var toSwitch in descendantCoins)
				{
					toSwitch.Height = Height.Mempool;
				}
			}
		}
	}

	public bool IsKnown(uint256 txid)
	{
		lock (Lock)
		{
			return KnownTransactions.Contains(txid);
		}
	}

	public bool TryGetSpenderByOutPoint(OutPoint outPoint, [NotNullWhen(true)] out SmartTransaction? spender)
	{
		lock (Lock)
		{
			spender = null;
			if (OutpointCoinCache.TryGetValue(outPoint, out var coin))
			{
				spender = coin.SpenderTransaction;
			}

			return spender is not null;
		}
	}

	public bool TryGetSpentCoinByOutPoint(OutPoint outPoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		lock (Lock)
		{
			return SpentCoinsByOutPoint.TryGetValue(outPoint, out coin);
		}
	}

	internal (ICoinsView toRemove, ICoinsView toAdd) Undo(uint256 txId)
	{
		lock (Lock)
		{
			var allCoins = AsAllCoinsViewNoLock();
			var toRemove = new List<SmartCoin>();
			var toAdd = new List<SmartCoin>();

			// remove recursively the coins created by the transaction
			foreach (SmartCoin createdCoin in allCoins.CreatedBy(txId))
			{
				toRemove.AddRange(RemoveNoLock(createdCoin));
			}

			// destroyed (spent) coins are now (unspent)
			foreach (SmartCoin destroyedCoin in allCoins.SpentBy(txId))
			{
				if (SpentCoins.Remove(destroyedCoin))
				{
					SpentCoinsByOutPoint.Remove(destroyedCoin.Outpoint);
					Coins.Add(destroyedCoin);
					toAdd.Add(destroyedCoin);
				}
			}

			KnownTransactions.Remove(txId);

			InvalidateSnapshot = true;

			return (new CoinsView(toRemove), new CoinsView(toAdd));
		}
	}

	public IEnumerable<SmartCoin> GetMyInputs(SmartTransaction transaction)
	{
		var inputs = transaction.Transaction.Inputs.Select(x => x.PrevOut).ToArray();

		var myInputs = new List<SmartCoin>();
		lock (Lock)
		{
			foreach (var input in inputs)
			{
				if (OutpointCoinCache.TryGetValue(input, out var coin))
				{
					myInputs.Add(coin);
				}
			}
		}

		return myInputs;
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

	public ICoinsView ChildrenOf(SmartCoin coin) => AsCoinsView().ChildrenOf(coin);

	public ICoinsView CoinJoinInProcess() => AsCoinsView().CoinJoinInProcess();

	public ICoinsView Confirmed() => AsCoinsView().Confirmed();

	public ICoinsView DescendantOf(SmartCoin coin) => AsCoinsView().DescendantOf(coin);

	private ICoinsView DescendantOfAndSelfNoLock(SmartCoin coin) => AsCoinsViewNoLock().DescendantOfAndSelf(coin);

	public ICoinsView DescendantOfAndSelf(SmartCoin coin) => AsCoinsView().DescendantOfAndSelf(coin);

	public ICoinsView FilterBy(Func<SmartCoin, bool> expression) => AsCoinsView().FilterBy(expression);

	public IEnumerator<SmartCoin> GetEnumerator() => AsCoinsView().GetEnumerator();

	public ICoinsView OutPoints(ISet<OutPoint> outPoints) => AsCoinsView().OutPoints(outPoints);

	public ICoinsView OutPoints(TxInList txIns) => AsCoinsView().OutPoints(txIns);

	public ICoinsView CreatedBy(uint256 txid) => AsCoinsView().CreatedBy(txid);

	public ICoinsView SpentBy(uint256 txid) => AsSpentCoinsView().SpentBy(txid);

	public SmartCoin[] ToArray() => AsCoinsView().ToArray();

	public Money TotalAmount() => AsCoinsView().TotalAmount();

	public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

	public ICoinsView Unspent() => AsCoinsView().Unspent();

	IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();
}
