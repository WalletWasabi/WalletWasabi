using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Send",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SendViewModel : RoutableViewModel, IValidatableViewModel
{
	private readonly CoinJoinManager? _coinJoinManager;
	private readonly TransactionInfo _transactionInfo;
	private readonly Wallet _wallet;
	[AutoNotify] private decimal _amountBtc;
	[AutoNotify] private decimal _exchangeRate;

	public SendViewModel(
		Wallet wallet,
		IObservable<Unit> balanceChanged,
		ObservableCollection<HistoryItemViewModelBase> history)
	{
		_wallet = wallet;
		_transactionInfo = new TransactionInfo(wallet.KeyManager.AnonScoreTarget);
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		ExchangeRate = _wallet.Synchronizer.UsdExchangeRate;
		Balance = new WalletBalanceTileViewModel(wallet, balanceChanged, history);

		SetupCancel(true, true, true);
		EnableBack = false;

		Func<decimal, bool> isAmountValid = a => a <= wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC);
		InitializeViewModels(wallet.Network, isAmountValid);

		AdvancedOptionsCommand = ReactiveCommand.CreateFromTask(
			async () =>
				await NavigateDialogAsync(
					new AdvancedSendOptionsViewModel(_transactionInfo),
					NavigationTarget.CompactDialogScreen));

		NextCommand = ReactiveCommand.CreateFromTask(
			() => OnNext(wallet),
			PaymentViewModel.IsValid());

		// TODO: Add this feature
		//this.WhenAnyValue(x => x.ConversionReversed)
		//	.Skip(1)
		//	.Subscribe(x => Services.UiConfig.SendAmountConversionReversed = x);
	}

	private void InitializeViewModels(Network network, Func<decimal, bool> isAmountValid)
	{
		var clipboard = new ClipboardObserver();
		IAddressParser parser  = new FullAddressParser(network);
		var newContentsChanged = clipboard.ContentChanged;
		IMutableAddressHost mutableAddressHost = new MutableAddressHost(parser);
		var contentChecker = new ContentChecker<string>(
			newContentsChanged,
			mutableAddressHost.TextChanged,
			s => parser.GetAddress(s) is not null);
		PaymentViewModel = new PaymentViewModel(
			newContentsChanged,
			mutableAddressHost,
			contentChecker,
			isAmountValid);
		ScanQrViewModel = new ScanQrViewModel(network, WebcamQrReader.IsOsPlatformSupported);
		PasteController = new PasteButtonViewModel(clipboard.ContentChanged, contentChecker);
	}

	public ScanQrViewModel ScanQrViewModel { get; set; }

	public PaymentViewModel PaymentViewModel { get; set; }

	public ICommand AdvancedOptionsCommand { get; }

	public WalletBalanceTileViewModel Balance { get; }

	public ValidationContext ValidationContext { get; } = new();

	public PasteButtonViewModel PasteController { get; set; }

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

		PaymentViewModel = Factory
			.Create(new FullAddressParser(_wallet.Network), a => a <= _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC))
			.DisposeWith(disposables);

		ScanQrViewModel = new ScanQrViewModel(_wallet.Network, WebcamQrReader.IsOsPlatformSupported);

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
}
