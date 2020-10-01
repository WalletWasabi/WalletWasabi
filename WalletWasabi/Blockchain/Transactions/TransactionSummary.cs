using NBitcoin;
using System;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions
{
	public class TransactionSummary
	{
		public DateTimeOffset DateTime { get; set; }
		public Height Height { get; set; }
		public Money Amount { get; set; }
		public SmartLabel Label { get; set; }
		public uint256 TransactionId { get; set; }
		public int BlockIndex { get; set; }
		public bool IsLikelyCoinJoinOutput { get; set; }
	}
}
