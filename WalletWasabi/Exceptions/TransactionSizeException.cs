using NBitcoin;

namespace WalletWasabi.Exceptions;

public class TransactionSizeException : Exception
{
	public TransactionSizeException(Money target, Money maximumPossible)
		: base($"Transaction size over the limit. Needed {target.ToString(false, true)} BTC. The maximum amount you can spent your coins is {maximumPossible.ToString(false, true)} BTC at the moment.")
	{
		Target = target ?? Money.Zero;
		MaximumPossible = maximumPossible ?? Money.Zero;
	}

	public Money Target { get; }
	public Money MaximumPossible { get; }
}
