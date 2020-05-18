using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.TransactionOutputs
{
	public class SmartInput
	{
		public SmartInput(Coin coin, bool isConfirmed)
		{
			Coin = Guard.NotNull(nameof(coin), coin);
			IsConfirmed = isConfirmed;
		}

		public Coin Coin { get; }
		public bool IsConfirmed { get; set; }
	}
}
