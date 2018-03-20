using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Exceptions
{
	public class InsufficientBalanceException : Exception
	{
		public InsufficientBalanceException(Money minimum, Money actual) : base($"Needed: {minimum.ToString(false, true)} BTC, got only: {actual.ToString(false, true)} BTC.")
		{

		}
	}
}
