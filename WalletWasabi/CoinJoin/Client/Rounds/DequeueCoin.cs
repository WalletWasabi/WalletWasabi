using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Client.Rounds
{
	public class DequeueCoin
	{
		public DequeueCoin(SmartCoin coin, string reason)
		{
			Coin = Guard.NotNull(nameof(coin), coin);
			Reason = Guard.Correct(reason);
		}

		public SmartCoin Coin { get; }
		public string Reason { get; }

		public bool HasReason => Reason.Length != 0;
	}
}
