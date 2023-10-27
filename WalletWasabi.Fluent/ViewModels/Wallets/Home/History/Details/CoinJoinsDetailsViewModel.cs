using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinsDetailsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly TransactionModel _transaction;

	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";
	[AutoNotify] private string _coinJoinFeeRawString = "";
	[AutoNotify] private string _coinJoinFeeString = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private ObservableCollection<uint256>? _transactionIds;
	[AutoNotify] private int _txCount;

	public CoinJoinsDetailsViewModel(UiContext uiContext, IWalletModel wallet, TransactionModel transaction)
	{
		_wallet = wallet;
		_transaction = transaction;

		UiContext = uiContext;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;

		ConfirmationTime = _transaction.TransactionSummary.TryGetConfirmationTime(out var estimation) ? estimation : null;
		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
	}

	public TimeSpan? ConfirmationTime { get; set; }

	public bool IsConfirmationTimeVisible { get; set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Transactions.TransactionProcessed
							.Do(Update)
							.Subscribe()
							.DisposeWith(disposables);
	}

	private void Update()
	{
		Date = _transaction.DateString;
		Status = _transaction.IsConfirmed ? "Confirmed" : "Pending";
		CoinJoinFeeAmount = _wallet.AmountProvider.Create(_transaction.OutgoingAmount);

		TransactionIds = new ObservableCollection<uint256>(_transaction.Children.Select(x => x.Id));
		TxCount = TransactionIds.Count;
	}
}
