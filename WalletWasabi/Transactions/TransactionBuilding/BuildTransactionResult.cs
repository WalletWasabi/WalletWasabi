using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Coins;
using WalletWasabi.Helpers;
using WalletWasabi.Transactions;

namespace WalletWasabi.Transactions.TransactionBuilding
{
	public class BuildTransactionResult
	{
		public SmartTransaction Transaction { get; }
		public PSBT Psbt { get; }
		public bool SpendsUnconfirmed { get; }
		public bool Signed { get; }
		public Money Fee { get; }
		public decimal FeePercentOfSent { get; }
		public IEnumerable<SmartCoin> OuterWalletOutputs { get; }
		public IEnumerable<SmartCoin> InnerWalletOutputs { get; }
		public IEnumerable<SmartCoin> SpentCoins { get; }

		public BuildTransactionResult(SmartTransaction transaction, PSBT psbt, bool spendsUnconfirmed, bool signed, Money fee, decimal feePercentOfSent, IEnumerable<SmartCoin> outerWalletOutputs, IEnumerable<SmartCoin> innerWalletOutputs, IEnumerable<SmartCoin> spentCoins)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
			Psbt = Guard.NotNull(nameof(psbt), psbt);
			SpendsUnconfirmed = spendsUnconfirmed;
			Signed = signed;
			Fee = fee ?? Money.Zero;
			FeePercentOfSent = feePercentOfSent;
			OuterWalletOutputs = outerWalletOutputs ?? new List<SmartCoin>();
			InnerWalletOutputs = innerWalletOutputs ?? new List<SmartCoin>();
			SpentCoins = Guard.NotNullOrEmpty(nameof(spentCoins), spentCoins);
		}
	}
}
