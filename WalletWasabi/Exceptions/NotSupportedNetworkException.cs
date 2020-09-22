using NBitcoin;
using System;

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
