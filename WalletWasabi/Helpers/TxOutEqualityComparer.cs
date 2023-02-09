using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Helpers;

public class TxOutEqualityComparer : IEqualityComparer<TxOut>
{
	public static readonly TxOutEqualityComparer Default = new();

	public bool Equals(TxOut? x, TxOut? y) => (x?.Value, x?.ScriptPubKey) == (y?.Value, y?.ScriptPubKey);

	public int GetHashCode(TxOut txOut) => (txOut.Value, txOut.ScriptPubKey).GetHashCode();
}

public class CoinEqualityComparer : IEqualityComparer<Coin>
{
	public static readonly CoinEqualityComparer Default = new();

	public bool Equals(Coin? x, Coin? y) => x?.Outpoint == y?.Outpoint;

	public int GetHashCode(Coin coin) => coin.Outpoint.GetHashCode();
}
