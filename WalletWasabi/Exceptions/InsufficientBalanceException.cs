using NBitcoin;

namespace WalletWasabi.Exceptions;

public class InsufficientBalanceException : Exception
{
	public InsufficientBalanceException(Money minimum, Money actual) : base($"Needed: {minimum.ToString(false, true)} BTC, got only: {actual.ToString(false, true)} BTC.")
	{
		Minimum = minimum ?? Money.Zero;
		Actual = actual ?? Money.Zero;
	}

	public Money Minimum { get; }
	public Money Actual { get; }
}
