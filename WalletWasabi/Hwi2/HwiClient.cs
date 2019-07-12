using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Hwi2
{
	public class HwiClient
	{
		public Network Network { get; }

		public HwiClient(Network network)
		{
			Network = Guard.NotNull(nameof(network), network);
		}
	}
}
