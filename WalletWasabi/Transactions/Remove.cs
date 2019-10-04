using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Transactions
{
	public class Remove : ITxStoreOperation
	{
		public IEnumerable<uint256> Transactions { get; }

		public Remove(IEnumerable<uint256> transactions)
		{
			Transactions = transactions;
		}
	}
}
