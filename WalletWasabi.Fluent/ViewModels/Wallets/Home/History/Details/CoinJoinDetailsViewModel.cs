using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoin Details", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinDetailsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private string _date = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private uint256 _transactionId;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;

	public CoinJoinDetailsViewModel(UiContext uiContext, IWalletModel wallet, TransactionModel transaction)
	{
		_wallet = wallet;

		_transactionId = transaction.Id;

		UiContext = uiContext;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;

		ConfirmationTime = transaction.TransactionSummary.TryGetConfirmationTime(out var estimation) ? estimation : null;
		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
	}

	public TimeSpan? ConfirmationTime { get; set; }

	public bool IsConfirmationTimeVisible { get; set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Transactions.TransactionProcessed
							.Subscribe(_ => Update())
							.DisposeWith(disposables);
	}

	private void Update()
	{
		if (_wallet.Transactions.TryGetById(TransactionId, out var transaction))
		{
			Date = transaction.DateString;
			CoinJoinFeeAmount = UiContext.AmountProvider.Create(transaction.OutgoingAmount);
			Confirmations = transaction.Confirmations;
			IsConfirmed = Confirmations > 0;
			TransactionId = transaction.Id;
		}
	}
}
