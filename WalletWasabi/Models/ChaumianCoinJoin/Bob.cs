using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Bob
	{
		public BitcoinAddress ActiveOutputAddress { get; }

		public Bob(BitcoinAddress activeOutputAddress)
		{
			ActiveOutputAddress = Guard.NotNull(nameof(activeOutputAddress), activeOutputAddress);
		}
	}
}
