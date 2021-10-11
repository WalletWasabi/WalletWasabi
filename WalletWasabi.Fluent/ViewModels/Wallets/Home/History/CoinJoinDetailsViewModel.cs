using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "CoinJoin Details")]
	public partial class CoinJoinDetailsViewModel : RoutableViewModel
	{
		private readonly CoinJoinsHistoryItemViewModel _coinJoinGroup;

		[AutoNotify] private string _date = "";
		[AutoNotify] private string _status = "";
		[AutoNotify] private Money? _coinJoinFee;
		[AutoNotify] private string _transactionIdString = "";

		public CoinJoinDetailsViewModel(CoinJoinsHistoryItemViewModel coinJoinGroup)
		{
			_coinJoinGroup = coinJoinGroup;

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
			NextCommand = CancelCommand;

			Update();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Observable
				.FromEventPattern(_coinJoinGroup, nameof(PropertyChanged))
				.Throttle(TimeSpan.FromMilliseconds(100))
				.Subscribe(_ => Update())
				.DisposeWith(disposables);
		}

		private void Update()
		{
			Date = _coinJoinGroup.DateString;
			Status = _coinJoinGroup.IsConfirmed ? "Confirmed" : "Pending";
			CoinJoinFee = _coinJoinGroup.OutgoingAmount;

			var transactionIds = _coinJoinGroup.CoinJoinTransactions.Select(x => x.TransactionId);
			TransactionIdString = string.Join('\n', transactionIds);
		}
	}
}
