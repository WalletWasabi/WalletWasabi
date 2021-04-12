using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "Transaction Details")]
	public partial class TransactionDetailsViewModel : RoutableViewModel
	{
		public TransactionDetailsViewModel(HistoryItemViewModel historyItem)
		{
			HistoryItem = historyItem;

			NextCommand = ReactiveCommand.Create(OnNext);
		}

		private void OnNext()
		{
			Navigate().Clear();
		}

		public HistoryItemViewModel HistoryItem { get; }
	}
}
