using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.BitcoinCore.Processes
{
	public class BitcoindException : Exception
	{
		public BitcoindException(string message) : base(message)
		{
		}
	}
}
