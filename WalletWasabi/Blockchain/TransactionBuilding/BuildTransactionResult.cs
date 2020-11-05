using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.TransactionBuilding
{
	public class BuildTransactionResult
	{
		public BuildTransactionResult(SmartTransaction transaction, PSBT psbt, bool spendsUnconfirmed, bool signed, Money fee, decimal feePercentOfSent, IEnumerable<Coin> outerWalletOutputs, IEnumerable<SmartCoin> innerWalletOutputs, IEnumerable<SmartCoin> spentCoins)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
			Psbt = Guard.NotNull(nameof(psbt), psbt);
			SpendsUnconfirmed = spendsUnconfirmed;
			Signed = signed;
			Fee = fee ?? Money.Zero;
			FeePercentOfSent = feePercentOfSent;
			OuterWalletOutputs = outerWalletOutputs;
			InnerWalletOutputs = innerWalletOutputs;
			SpentCoins = spentCoins;
		}

		public SmartTransaction Transaction { get; }
		public PSBT Psbt { get; }
		public bool SpendsUnconfirmed { get; }
		public bool Signed { get; }
		public Money Fee { get; }
		public decimal FeePercentOfSent { get; }
		public IEnumerable<Coin> OuterWalletOutputs { get; }
		public IEnumerable<SmartCoin> InnerWalletOutputs { get; }
		public IEnumerable<SmartCoin> SpentCoins { get; }
	}
}
