using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using Constants = WalletWasabi.Helpers.Constants;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using System.Reactive;
using System.Collections.ObjectModel;
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
	private readonly object _parsingLock = new();
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly CoinJoinManager? _coinJoinManager;
	[AutoNotify] private string _to;
	[AutoNotify] private decimal _amountBtc;
	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private bool _isFixedAmount;
	[AutoNotify] private bool _isPayJoin;
	[AutoNotify] private string? _payJoinEndPoint;
	[AutoNotify] private bool _conversionReversed;

	public SendViewModel(Wallet wallet, IObservable<Unit> balanceChanged, ObservableCollection<HistoryItemViewModelBase> history)
	{
		_to = "";
		_wallet = wallet;
		_transactionInfo = new TransactionInfo(wallet.KeyManager.AnonScoreTarget);
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		_conversionReversed = Services.UiConfig.SendAmountConversionReversed;

		IsQrButtonVisible = WebcamQrReader.IsOsPlatformSupported;

		ExchangeRate = _wallet.Synchronizer.UsdExchangeRate;

		Balance = new WalletBalanceTileViewModel(wallet, balanceChanged, history);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		this.ValidateProperty(x => x.To, ValidateToField);
		this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(ParseToField);

		this.WhenAnyValue(x => x.PayJoinEndPoint)
			.Subscribe(endPoint =>
			{
				if (endPoint is { })
				{
					_transactionInfo.PayJoinClient = GetPayjoinClient(endPoint);
					IsPayJoin = true;
				}
				else
				{
					IsPayJoin = false;
				}
			});

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(async () => await OnAutoPasteAsync());
		QrCommand = ReactiveCommand.Create(async () =>
		{
			ShowQrCameraDialogViewModel dialog = new(_wallet.Network);
			var result = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);
			if (!string.IsNullOrWhiteSpace(result.Result))
			{
				To = result.Result;
			}
		});

		AdvancedOptionsCommand = ReactiveCommand.CreateFromTask(async () =>
			await NavigateDialogAsync(new AdvancedSendOptionsViewModel(_transactionInfo), NavigationTarget.CompactDialogScreen));

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.AmountBtc, x => x.To)
				.Select(tup =>
				{
					var (amountBtc, to) = tup;
					var allFilled = !string.IsNullOrEmpty(to) && amountBtc > 0;
					var hasError = Validations.Any;

					return allFilled && !hasError;
				});

		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var address = BitcoinAddress.Create(To, wallet.Network);

			_transactionInfo.Reset();
			_transactionInfo.Amount = new Money(AmountBtc, MoneyUnit.BTC);

			var labelDialog = new LabelEntryDialogViewModel(_wallet, _transactionInfo);
			var result = await NavigateDialogAsync(labelDialog, NavigationTarget.CompactDialogScreen);
			if (result.Result is not { } label)
			{
				return;
			}

			_transactionInfo.UserLabels = label;

			Navigate().To(new TransactionPreviewViewModel(wallet, _transactionInfo, address, _isFixedAmount));
		}, nextCommandCanExecute);

		this.WhenAnyValue(x => x.ConversionReversed)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.SendAmountConversionReversed = x);
	}

	public bool IsQrButtonVisible { get; }

	public ICommand PasteCommand { get; }

	public ICommand AutoPasteCommand { get; }

	public ICommand QrCommand { get; }

	public ICommand AdvancedOptionsCommand { get; }

	public WalletBalanceTileViewModel Balance { get; }

	private async Task OnAutoPasteAsync()
	{
		var isAutoPasteEnabled = Services.UiConfig.AutoPaste;

		if (string.IsNullOrEmpty(To) && isAutoPasteEnabled)
		{
			await OnPasteAsync(pasteIfInvalid: false);
		}
	}

	private async Task OnPasteAsync(bool pasteIfInvalid = true)
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			var text = await clipboard.GetTextAsync();

			lock (_parsingLock)
			{
				if (!TryParseUrl(text) && pasteIfInvalid)
				{
					To = text;
				}
			}
		}
	}

	private IPayjoinClient? GetPayjoinClient(string endPoint)
	{
		if (!string.IsNullOrWhiteSpace(endPoint) &&
			Uri.IsWellFormedUriString(endPoint, UriKind.Absolute))
		{
			var payjoinEndPointUri = new Uri(endPoint);
			if (!Services.Config.UseTor)
			{
				if (payjoinEndPointUri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogWarning("Payjoin server is an onion service but Tor is disabled. Ignoring...");
					return null;
				}

				if (Services.Config.Network == Network.Main && payjoinEndPointUri.Scheme != Uri.UriSchemeHttps)
				{
					Logger.LogWarning("Payjoin server is not exposed as an onion service nor https. Ignoring...");
					return null;
				}
			}

			IHttpClient httpClient = Services.HttpClientFactory.NewHttpClient(() => payjoinEndPointUri, Mode.DefaultCircuit);
			return new PayjoinClient(payjoinEndPointUri, httpClient);
		}

		return null;
	}

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

	private void ValidateToField(IValidationErrors errors)
	{
		if (!string.IsNullOrEmpty(To) && (To.IsTrimmable() || !AddressStringParser.TryParse(To, _wallet.Network, out _)))
		{
			errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
		}
		else if (IsPayJoin && _wallet.KeyManager.IsHardwareWallet)
		{
			errors.Add(ErrorSeverity.Error, "Payjoin is not possible with hardware wallets.");
		}
	}

	private void ParseToField(string s)
	{
		lock (_parsingLock)
		{
			Dispatcher.UIThread.Post(() => TryParseUrl(s));
		}
	}

	private bool TryParseUrl(string? text)
	{
		text = text?.Trim();

		if (string.IsNullOrEmpty(text))
		{
			return false;
		}

		bool result = false;

		if (AddressStringParser.TryParse(text, _wallet.Network, out BitcoinUrlBuilder? url))
		{
			result = true;
			if (url.Label is { } label)
			{
				_transactionInfo.UserLabels = new SmartLabel(label);
			}

			if (url.UnknownParameters.TryGetValue("pj", out var endPoint))
			{
				PayJoinEndPoint = endPoint;
			}
			else
			{
				PayJoinEndPoint = null;
			}

			if (url.Address is { })
			{
				To = url.Address.ToString();
			}

			if (url.Amount is { })
			{
				AmountBtc = url.Amount.ToDecimal(MoneyUnit.BTC);
				IsFixedAmount = true;
			}
			else
			{
				IsFixedAmount = false;
			}
		}
		else
		{
			IsFixedAmount = false;
			PayJoinEndPoint = null;
		}

		return result;
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		if (!inHistory)
		{
			To = "";
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

		RxApp.MainThreadScheduler.Schedule(async () => await OnAutoPasteAsync());

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
