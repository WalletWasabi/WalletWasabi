using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsView : ICoinsView
{
	public CoinsView(IEnumerable<SmartCoin> coins)
	{
		Coins = coins;
	}

	private IEnumerable<SmartCoin> Coins { get; }

	public ICoinsView Unspent() => new CoinsView(Coins.Where(x => !x.IsSpent() && !x.SpentAccordingToBackend));

	public ICoinsView Available() => new CoinsView(Coins.Where(x => x.IsAvailable()));

	public ICoinsView Confirmed() => new CoinsView(Coins.Where(x => x.Confirmed));

	public ICoinsView Unconfirmed() => new CoinsView(Coins.Where(x => !x.Confirmed));

	public ICoinsView AtBlockHeight(Height height) => new CoinsView(Coins.Where(x => x.Height == height));

	public ICoinsView CreatedBy(uint256 txid) => new CoinsView(Coins.Where(x => x.TransactionId == txid));

	public ICoinsView SpentBy(uint256 txid) => new CoinsView(Coins.Where(x => x.SpenderTransaction is { } && x.SpenderTransaction.GetHash() == txid));

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		coin = Coins.FirstOrDefault(x => x.Outpoint == outpoint);
		if (coin is null)
		{
			return false;
		}
		else
		{
			return true;
		}
	}

	public Money TotalAmount() => Coins.Sum(x => x.Amount);

	public IEnumerator<SmartCoin> GetEnumerator() => Coins.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => Coins.GetEnumerator();
}
