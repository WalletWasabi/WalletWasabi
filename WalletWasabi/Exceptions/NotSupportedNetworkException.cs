using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Exceptions
{
	public class NotSupportedNetworkException : NotSupportedException
	{
		public NotSupportedNetworkException(Network network)
			: base($"{nameof(Network)} not supported: {network}.")
		{
		}
	}
}
