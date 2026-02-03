using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsView : ICoinsView
{
	public CoinsView(IEnumerable<SmartCoin> coins)
	{
		_coins = coins;
	}

	private readonly IEnumerable<SmartCoin> _coins;

	public ICoinsView Unspent() => new CoinsView(_coins.Where(x => !x.IsSpent()));

	public ICoinsView Available() => new CoinsView(_coins.Where(x => x.IsAvailable()));

	public ICoinsView Confirmed() => new CoinsView(_coins.Where(x => x.Confirmed));

	public ICoinsView Unconfirmed() => new CoinsView(_coins.Where(x => !x.Confirmed));

	public ICoinsView CreatedBy(uint256 txid) => new CoinsView(_coins.Where(x => x.TransactionId == txid));

	public ICoinsView SpentBy(uint256 txid) => new CoinsView(_coins.Where(x => x.SpenderTransaction is { } && x.SpenderTransaction.GetHash() == txid));

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		coin = _coins.FirstOrDefault(x => x.Outpoint == outpoint);
		if (coin is null)
		{
			return false;
		}
		else
		{
			return true;
		}
	}

	public Money TotalAmount() => _coins.Sum(x => x.Amount);

	public IEnumerator<SmartCoin> GetEnumerator() => _coins.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _coins.GetEnumerator();
}
