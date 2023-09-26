using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoins")]
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
		Date = _coinJoinGroup.DateString;
		Status = _coinJoinGroup.IsConfirmed ? "Confirmed" : "Pending";
		CoinJoinFeeAmount = UiContext.CreateAmount(_coinJoinGroup.OutgoingAmount);

		TransactionIds = new ObservableCollection<uint256>(_coinJoinGroup.CoinJoinTransactions.Select(x => x.GetHash()));
		TxCount = TransactionIds.Count;
	}
}
