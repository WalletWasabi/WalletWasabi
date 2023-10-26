using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoin Details", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinDetailsViewModel : RoutableViewModel
{
	private readonly CoinJoinHistoryItemViewModel _coinJoin;
	private readonly IObservable<Unit> _updateTrigger;

	[AutoNotify] private string _date = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private uint256? _transactionId;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;

	public CoinJoinDetailsViewModel(UiContext uiContext, CoinJoinHistoryItemViewModel coinJoin, IObservable<Unit> updateTrigger)
	{
		UiContext = uiContext;
		_coinJoin = coinJoin;
		_updateTrigger = updateTrigger;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
		ConfirmationTime = coinJoin.Transaction.TransactionSummary.TryGetConfirmationTime(out var estimation) ? estimation : null;
		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;

		Update();
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
		Date = _coinJoin.Transaction.DateString;
		CoinJoinFeeAmount = UiContext.AmountProvider.Create(_coinJoin.Transaction.OutgoingAmount);
		Confirmations = _coinJoin.Transaction.Confirmations;
		IsConfirmed = Confirmations > 0;

		TransactionId = _coinJoin.Transaction.Id;
	}
}
