using NBitcoin;
using System;
using WalletWasabi.Models;

namespace WalletWasabi.Models
{
	public class TransactionSummaryData
	{
		public DateTimeOffset DateTime { get; set; }
		public Height Height { get; set; }
		public Money Amount { get; set; }
		public string Label { get; set; }
		public uint256 TransactionId { get; set; }
	}
}
