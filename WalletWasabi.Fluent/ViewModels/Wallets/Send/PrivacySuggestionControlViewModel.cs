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

		public PrivacySuggestionControlViewModel(BuildTransactionResult transactionResult)
		{
			_transactionResult = transactionResult;
		}

		[AutoNotify] private string _title;
		[AutoNotify] private string _caption;
		[AutoNotify] private string[] _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;
	}
}