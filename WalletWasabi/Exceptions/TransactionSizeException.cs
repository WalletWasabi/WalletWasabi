using NBitcoin;

namespace WalletWasabi.Exceptions;

public class TransactionSizeException : Exception
{
	public TransactionSizeException(Money minimum, Money actual)
		: base($"Transaction size over the limit. Needed {minimum.ToString(false, true)} BTC. The maximum amount you can spent your coins is {actual.ToString(false, true)} BTC at the moment.")
	{
		Minimum = minimum ?? Money.Zero;
		Actual = actual ?? Money.Zero;
	}

	public Money Minimum { get; }
	public Money Actual { get; }
}
