using System.Collections.Generic;
using WalletWasabi.Helpers;
using NBitcoin;

namespace WalletWasabi.Models
{
	public class BuildTransactionResult
	{
		public SmartTransaction Transaction { get; }
		public bool SpendsUnconfirmed { get; }
		public Money Fee { get; }
		public decimal FeePercentOfSent { get; }
		public IEnumerable<SmartCoin> OuterWalletOutputs { get; }
		public IEnumerable<SmartCoin> InnerWalletOutputs { get; }
		public IEnumerable<SmartCoin> SpentCoins { get; }

		public BuildTransactionResult(SmartTransaction transaction, bool spendsUnconfirmed, Money fee, decimal feePercentOfSent, IEnumerable<SmartCoin> outerWalletOutputs, IEnumerable<SmartCoin> innerWalletOutputs, IEnumerable<SmartCoin> spentCoins)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
			SpendsUnconfirmed = spendsUnconfirmed;
			Fee = fee ?? Money.Zero;
			FeePercentOfSent = feePercentOfSent;
			OuterWalletOutputs = outerWalletOutputs ?? new List<SmartCoin>();
			InnerWalletOutputs = innerWalletOutputs ?? new List<SmartCoin>();
			SpentCoins = Guard.NotNullOrEmpty(nameof(spentCoins), spentCoins);
		}
	}
}
