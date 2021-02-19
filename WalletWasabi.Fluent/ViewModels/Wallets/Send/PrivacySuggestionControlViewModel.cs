using System.Linq;
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

		public PrivacySuggestionControlViewModel(BuildTransactionResult transactionResult, PrivacyOptimisationLevel optimisationLevel, params string[] benefits)
		{
			_transactionResult = transactionResult;
			_optimisationLevel = optimisationLevel;
			_benefits = benefits;

			var total = transactionResult.OuterWalletOutputs.Sum(x => x.Amount);
			var fee = transactionResult.Fee;
			var feePercent = transactionResult.FeePercentOfSent;

			_title = $"{total} BTC";
			_caption = $"Fee: {feePercent}% ({fee})";
		}

		[AutoNotify] private string _title;
		[AutoNotify] private string _caption;
		[AutoNotify] private string[] _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;
	}
}