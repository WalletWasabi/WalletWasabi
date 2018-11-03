using NBitcoin;
using System;

namespace WalletWasabi.Exceptions
{
	public class InsufficientBalanceException : Exception
	{
		public InsufficientBalanceException(Money minimum, Money actual) : base($"Needed: {minimum.ToString(false, true)} BTC for this transaction but only: {actual.ToString(false, true)} BTC was selected to send.")
		{
		}
	}
}
