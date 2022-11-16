using NBitcoin;

namespace WalletWasabi.Exceptions;

public class TransactionSizeException : Exception
{
	public TransactionSizeException(Money target, Money maximumPossible)
		: base($"Transaction size is over the limit, {target.ToString(false, true)} BTC was needed. Currently, the maximum amount you can spend is {maximumPossible.ToString(false, true)} BTC.")
	{
		Target = target ?? Money.Zero;
		MaximumPossible = maximumPossible ?? Money.Zero;
	}

	public Money Target { get; }
	public Money MaximumPossible { get; }
}
