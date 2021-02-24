using System;
using System.Globalization;
using System.Linq;
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

		public PrivacySuggestionControlViewModel(decimal originalAmount, BuildTransactionResult transactionResult, PrivacyOptimisationLevel optimisationLevel, params string[] benefits)
		{
			_transactionResult = transactionResult;
			_optimisationLevel = optimisationLevel;
			_benefits = benefits;

			decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			if (optimisationLevel == PrivacyOptimisationLevel.Better)
			{
				var pcDifference = ((total - originalAmount) / originalAmount) * 100;

				_caption = pcDifference > 0 ? $"{pcDifference:F}% More" : $"{Math.Abs(pcDifference):F}% Less";
			}

			_title = $"{total} BTC";
		}

		[AutoNotify] private string _title;
		[AutoNotify] private string _caption;
		[AutoNotify] private string[] _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;

		public BuildTransactionResult TransactionResult => _transactionResult;
	}
}