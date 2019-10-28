using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Tests.NodeBuilding
{
	public class BitcoindException : Exception
	{
		public BitcoindException(string message) : base(message)
		{
		}
	}
}
