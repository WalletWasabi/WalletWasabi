using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using System.Reactive;
using System.Collections.ObjectModel;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Send",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SendViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly CoinJoinManager? _coinJoinManager;
	[AutoNotify] private decimal _amountBtc;
	[AutoNotify] private decimal _exchangeRate;

	public SendViewModel(Wallet wallet, IObservable<Unit> balanceChanged, ObservableCollection<HistoryItemViewModelBase> history)
	{
		_wallet = wallet;
		_transactionInfo = new TransactionInfo(wallet.KeyManager.AnonScoreTarget);
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		IsQrButtonVisible = WebcamQrReader.IsOsPlatformSupported;
		ExchangeRate = _wallet.Synchronizer.UsdExchangeRate;
		Balance = new WalletBalanceTileViewModel(wallet, balanceChanged, history);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

		PaymentViewModel = Factory.Create(new FullAddressParser(wallet.Network));

		QrCommand = ReactiveCommand.Create(async () =>
		{
			ShowQrCameraDialogViewModel dialog = new(_wallet.Network);
			var result = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);
			if (!string.IsNullOrWhiteSpace(result.Result))
			{
				PaymentViewModel.MutableAddressHost.Text = result.Result;
			}
		});

		AdvancedOptionsCommand = ReactiveCommand.CreateFromTask(async () =>
			await NavigateDialogAsync(new AdvancedSendOptionsViewModel(_transactionInfo), NavigationTarget.CompactDialogScreen));

		NextCommand = ReactiveCommand.CreateFromTask(
			() => OnNext(wallet),
			PaymentViewModel.MutableAddressHost.ParsedAddress.Select(x => x is not null));

		//this.WhenAnyValue(x => x.ConversionReversed)
		//	.Skip(1)
		//	.Subscribe(x => Services.UiConfig.SendAmountConversionReversed = x);
	}

	private async Task OnNext(Wallet wallet)
	{
		var address = BitcoinAddress.Create(PaymentViewModel.Address, wallet.Network);

		_transactionInfo.Reset();
		_transactionInfo.Amount = new Money(PaymentViewModel.Amount, MoneyUnit.BTC);

		var labelDialog = new LabelEntryDialogViewModel(_wallet, _transactionInfo);
		var result = await NavigateDialogAsync(labelDialog, NavigationTarget.CompactDialogScreen);
		if (result.Result is not { } label)
		{
			return;
		}

		_transactionInfo.UserLabels = label;

		var isFixedAmount = PaymentViewModel.EndPoint is not null;
		Navigate().To(new TransactionPreviewViewModel(wallet, _transactionInfo, address, isFixedAmount));
	}

	public PaymentViewModel PaymentViewModel { get; }

	public bool IsQrButtonVisible { get; }

	public ICommand QrCommand { get; }

	public ICommand AdvancedOptionsCommand { get; }

	public WalletBalanceTileViewModel Balance { get; }

	private void ValidateAmount(IValidationErrors errors)
	{
		if (AmountBtc > Constants.MaximumNumberOfBitcoins)
		{
			errors.Add(ErrorSeverity.Error, "Amount must be less than the total supply of BTC.");
		}
		else if (AmountBtc > _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC))
		{
			errors.Add(ErrorSeverity.Error, "Insufficient funds to cover the amount requested.");
		}
		else if (AmountBtc <= 0)
		{
			errors.Add(ErrorSeverity.Error, "Amount must be more than 0 BTC");
		}
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		if (!inHistory)
		{
			PaymentViewModel.MutableAddressHost.Text = "";
			AmountBtc = 0;
			ClearValidations();

			if (_coinJoinManager is { } coinJoinManager)
			{
				coinJoinManager.IsUserInSendWorkflow = true;
			}
		}

		_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => ExchangeRate = x)
			.DisposeWith(disposables);

		Balance.Activate(disposables);

		base.OnNavigatedTo(inHistory, disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);

		if (!isInHistory && _coinJoinManager is { } coinJoinManager)
		{
			coinJoinManager.IsUserInSendWorkflow = false;
		}
	}
}
