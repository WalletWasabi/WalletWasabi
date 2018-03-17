using System;
using System.Collections.Generic;
using System.Text;
using MagicalCryptoWallet.Helpers;
using NBitcoin;

namespace MagicalCryptoWallet.Models
{
	public class BuildTransactionResult
	{
		public SmartTransaction Transaction { get; }
		public bool SpendsUnconfirmed { get; }
		public Money Fee { get; }
		public decimal FeePercentOfSent { get; }
		public IEnumerable<SmartCoin> ExternalOutputs { get; }
		public IEnumerable<SmartCoin> InternalOutputs { get; }
		public IEnumerable<SmartCoin> SpentCoins { get; }

		public BuildTransactionResult(SmartTransaction transaction, bool spendsUnconfirmed, Money fee, decimal feePercentOfSent, IEnumerable<SmartCoin> externalOutputs, IEnumerable<SmartCoin> internalOutputs, IEnumerable<SmartCoin> spentCoins)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
			SpendsUnconfirmed = spendsUnconfirmed;
			Fee = fee ?? Money.Zero;
			FeePercentOfSent = feePercentOfSent;
			ExternalOutputs = externalOutputs ?? new List<SmartCoin>();
			InternalOutputs = internalOutputs ?? new List<SmartCoin>();
			SpentCoins = Guard.NotNullOrEmpty(nameof(spentCoins), spentCoins);
		}
	}
}
