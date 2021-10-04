using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "CoinJoin Details")]
	public partial class CoinJoinDetailsViewModel : RoutableViewModel
	{
		[AutoNotify] private string _date;
		[AutoNotify] private string _status;
		[AutoNotify] private Money? _coinJoinFee;
		[AutoNotify] private string _transactionIdString;

		public CoinJoinDetailsViewModel(CoinJoinsHistoryItemViewModel coinJoinGroup)
		{
			_date = coinJoinGroup.DateString;
			_status = coinJoinGroup.IsConfirmed ? "Confirmed" : "Pending";
			_coinJoinFee = coinJoinGroup.OutgoingAmount;

			var transactionIds = coinJoinGroup.CoinJoinTransactions.Select(x => x.TransactionId);
			_transactionIdString = string.Join('\n', transactionIds);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
			NextCommand = CancelCommand;
		}
	}
}
