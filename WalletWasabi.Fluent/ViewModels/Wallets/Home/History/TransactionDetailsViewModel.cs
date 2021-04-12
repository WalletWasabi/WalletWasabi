using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "Transaction Details")]
	public partial class TransactionDetailsViewModel : RoutableViewModel
	{
		public TransactionDetailsViewModel(HistoryItemViewModel historyItem)
		{
			HistoryItem = historyItem;
			EnableCancel = true;
		}

		public HistoryItemViewModel HistoryItem { get; }
	}
}
