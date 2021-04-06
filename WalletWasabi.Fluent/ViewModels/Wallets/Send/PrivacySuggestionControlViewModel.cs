using System;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public enum PrivacyOptimisationLevel
	{
		Standard,
		Better
	}

	public partial class PrivacySuggestionControlViewModel : ViewModelBase
	{
		private readonly BuildTransactionResult _transactionResult;
		[AutoNotify] private string _amount;
		[AutoNotify] private string _amountFiat;
		[AutoNotify] private string _caption;
		[AutoNotify] private string[] _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;

		public PrivacySuggestionControlViewModel(decimal originalAmount, BuildTransactionResult transactionResult, PrivacyOptimisationLevel optimisationLevel, decimal fiatExchangeRate, params string[] benefits)
		{
			_transactionResult = transactionResult;
			_optimisationLevel = optimisationLevel;
			_benefits = benefits;

			decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fiatTotal = total * fiatExchangeRate;

			_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");

			if (optimisationLevel == PrivacyOptimisationLevel.Better)
			{
				var fiatOriginal = originalAmount * fiatExchangeRate;
				var fiatDifference = fiatTotal - fiatOriginal;

				_caption = (fiatDifference > 0 ? $"{fiatDifference.GenerateFiatText("USD")} More" : $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
					.Replace("(", "").Replace(")", "");
			}
			else
			{
				_caption = "As Requested";
			}

			_amount = $"{total}";
		}

		public BuildTransactionResult TransactionResult => _transactionResult;
	}
}