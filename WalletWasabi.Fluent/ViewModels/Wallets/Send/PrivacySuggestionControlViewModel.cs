using System;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;

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

			decimal total;

			if (optimisationLevel == PrivacyOptimisationLevel.Better)
			{
				total = transactionResult.OuterWalletOutputs
					.Select(x => x.Amount)
					.Concat(transactionResult.InnerWalletOutputs.Select(x => x.Amount))
					.Sum().ToDecimal(MoneyUnit.BTC);

				var pcDifference = ((total - originalAmount) / originalAmount) * 100;

				_caption = pcDifference > 0 ? $"{pcDifference:F}% More" : $"{Math.Abs(pcDifference):F}% Less";
			}
			else
			{
				if (!transactionResult.OuterWalletOutputs.Any()) // self spend
				{
					total = transactionResult.InnerWalletOutputs
						.Where(x => !x.HdPubKey.IsInternal)
						.Select(x => x.Amount)
						.Sum().ToDecimal(MoneyUnit.BTC);
				}
				else
				{
					total = transactionResult.OuterWalletOutputs
						.Select(x => x.Amount)
						.Sum().ToDecimal(MoneyUnit.BTC);
				}
			}

			_title = $"{total} BTC";
		}

		[AutoNotify] private string _title;
		[AutoNotify] private string _caption;
		[AutoNotify] private string[] _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;
	}
}