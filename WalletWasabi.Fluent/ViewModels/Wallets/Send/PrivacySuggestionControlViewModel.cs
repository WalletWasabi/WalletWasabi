using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public enum PrivacyOptimisationLevel
	{
		Standard,
		Better
	}

	public partial class PrivacySuggestionControlViewModel : ViewModelBase
	{
		[AutoNotify] private string _amount;
		[AutoNotify] private string _amountFiat;
		[AutoNotify] private List<PrivacySuggestionBenefit> _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;
		[AutoNotify] private bool _optimisationLevelGood;

		public PrivacySuggestionControlViewModel(
			decimal originalAmount,
			BuildTransactionResult transactionResult,
			PrivacyOptimisationLevel optimisationLevel,
			decimal fiatExchangeRate,
			params PrivacySuggestionBenefit[] benefits)
		{
			TransactionResult = transactionResult;
			_optimisationLevel = optimisationLevel;
			_benefits = benefits.ToList();

			decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fiatTotal = total * fiatExchangeRate;

			_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");
			_optimisationLevelGood = optimisationLevel == PrivacyOptimisationLevel.Better;

			if (_optimisationLevelGood)
			{
				var fiatOriginal = originalAmount * fiatExchangeRate;
				var fiatDifference = fiatTotal - fiatOriginal;

				var difference = (fiatDifference > 0
						? $"{fiatDifference.GenerateFiatText("USD")} More"
						: $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
					.Replace("(", "").Replace(")", "");

				_benefits.Add(new (false, difference));
			}
			else
			{
				// This is just to pad the control.
				_benefits.Add(new (false, " "));
			}

			_amount = $"{total}";
		}

		public BuildTransactionResult TransactionResult { get; }
	}
}