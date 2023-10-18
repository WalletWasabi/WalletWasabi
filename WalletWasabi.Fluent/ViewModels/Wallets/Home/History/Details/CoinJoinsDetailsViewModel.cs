using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinsDetailsViewModel : RoutableViewModel
{
	private readonly CoinJoinsHistoryItemViewModel _coinJoinGroup;
	private readonly IObservable<Unit> _updateTrigger;

	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";
	[AutoNotify] private string _coinJoinFeeRawString = "";
	[AutoNotify] private string _coinJoinFeeString = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private ObservableCollection<uint256>? _transactionIds;
	[AutoNotify] private int _txCount;

	public CoinJoinsDetailsViewModel(UiContext uiContext, CoinJoinsHistoryItemViewModel coinJoinGroup, IObservable<Unit> updateTrigger)
	{
		UiContext = uiContext;
		_coinJoinGroup = coinJoinGroup;
		_updateTrigger = updateTrigger;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;

		Update();

		ConfirmationTime = TimeSpan.Zero; // TODO: Calculate confirmation time
		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
	}

	public TimeSpan? ConfirmationTime { get; set; }

	public bool IsConfirmationTimeVisible { get; set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_updateTrigger
			.Subscribe(_ => Update())
			.DisposeWith(disposables);
	}

	private void Update()
	{
		Date = _coinJoinGroup.Transaction.DateString;
		Status = _coinJoinGroup.Transaction.IsConfirmed ? "Confirmed" : "Pending";
		CoinJoinFeeAmount = UiContext.AmountProvider.Create(_coinJoinGroup.Transaction.OutgoingAmount);

		TransactionIds = new ObservableCollection<uint256>(_coinJoinGroup.Transaction.Children.Select(x => x.Id));
		TxCount = TransactionIds.Count;
	}
}
