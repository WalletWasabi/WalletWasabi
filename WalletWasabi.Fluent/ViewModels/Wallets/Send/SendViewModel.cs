using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Services;
using Address = WalletWasabi.Userfacing.Address;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Send",
	Caption = "Display wallet send dialog",
	IconName = "wallet_action_send",
	Order = 5,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Send", "Action", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class SendViewModel : RoutableViewModel
{
	private readonly object _parsingLock = new();
	private readonly Wallet _wallet;
	private readonly IWalletModel _walletModel;
	private readonly SendFlowModel _parameters;
	private readonly CoinJoinManager? _coinJoinManager;
	private readonly ClipboardObserver _clipboardObserver;

	private bool _parsingTo;
	private Address _parsedAddress;

	[AutoNotify] private string _caption = "";
	[AutoNotify] private string _to;
	[AutoNotify] private decimal? _amountBtc;
	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private bool _isFixedAmount;
	[AutoNotify] private bool _isPayJoin;
	[AutoNotify] private string? _payJoinEndPoint;
	[AutoNotify] private bool _conversionReversed;
	[AutoNotify] private bool _displaySilentPaymentInfo;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private SuggestionLabelsViewModel _suggestionLabels;
	[AutoNotify] private string _defaultLabel;
	[AutoNotify] private bool _isFixedAddress;


	public SendViewModel(UiContext uiContext, IWalletModel walletModel, SendFlowModel parameters)
	{
		UiContext = uiContext;
		_to = "";

		_wallet = parameters.Wallet;
		_walletModel = walletModel;
		_parameters = parameters;
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		_conversionReversed = Services.UiConfig.SendAmountConversionReversed;

		_exchangeRate = Services.Status.UsdExchangeRate;
		Services.EventBus.Subscribe<ExchangeRateChanged>(er => _exchangeRate = er.UsdBtcRate);

		Balance =
			_parameters.IsManual
			? Observable.Return(_walletModel.AmountProvider.Create(_parameters.AvailableAmount))
			: _walletModel.Balances;

		_suggestionLabels = new SuggestionLabelsViewModel(_walletModel, Intent.Send, 3);

		_defaultLabel = _parameters.Donate ? "Wasabi team" : "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = parameters.IsManual;

		this.ValidateProperty(x => x.To, ValidateToField);
		this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(ParseToField);

		this.WhenAnyValue(x => x.PayJoinEndPoint)
			.Subscribe(endPoint => IsPayJoin = endPoint is { });

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(OnAutoPasteAsync);
		InsertMaxCommand = ReactiveCommand.Create(() => AmountBtc = parameters.AvailableCoins.TotalAmount().ToDecimal(MoneyUnit.BTC));
		QrCommand = ReactiveCommand.Create(ShowQrCameraAsync);

		var nextCommandCanExecute =
			this.WhenAnyValue(
					x => x.AmountBtc,
					x => x.To,
					x => x.SuggestionLabels.Labels.Count,
					x => x.SuggestionLabels.IsCurrentTextValid)
				.Select(tup =>
				{
					var (amountBtc, to, labelsCount, isCurrentTextValid) = tup;
					var allFilled = !string.IsNullOrEmpty(to) && amountBtc > 0;
					var hasError = Validations.AnyErrors;

					return allFilled && !hasError && (labelsCount > 0 || isCurrentTextValid);
				});

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);

		this.WhenAnyValue(x => x.ConversionReversed)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.SendAmountConversionReversed = x);

		_clipboardObserver = new ClipboardObserver(Balance);
	}

	public IObservable<Amount> Balance { get; }

	public IObservable<string?> UsdContent => _clipboardObserver.ClipboardUsdContentChanged(RxApp.MainThreadScheduler);

	public IObservable<string?> BitcoinContent => _clipboardObserver.ClipboardBtcContentChanged(RxApp.MainThreadScheduler);

	public bool IsQrButtonVisible => UiContext.QrCodeReader.IsPlatformSupported;

	public bool IsNotInDonationWorkflow => !_parameters.Donate;

	public ICommand PasteCommand { get; }

	public ICommand AutoPasteCommand { get; }

	public ICommand QrCommand { get; }

	public ICommand InsertMaxCommand { get; }

	private async Task OnNextAsync()
	{
		var label = new LabelsArray(SuggestionLabels.Labels.ToArray());

		if (AmountBtc is not { } amountBtc)
		{
			return;
		}

		if (_parsedAddress is not { } parsedAddress)
		{
			return;
		}

		var amount = new Money(amountBtc, MoneyUnit.BTC);
		Destination destination = parsedAddress switch
		{
			Address.Bitcoin bitcoin => new Destination.Loudly(bitcoin.Address.ScriptPubKey),
			Address.Bip21Uri { Address: Address.Bitcoin bitcoin }  => new Destination.Loudly(bitcoin.Address.ScriptPubKey),
			Address.Bip21Uri { Address: Address.SilentPayment silentPayment }  => new Destination.Silent(silentPayment.Address),
			Address.SilentPayment silentPayment => new Destination.Silent(silentPayment.Address),
			_ => throw new ArgumentException("Unknown address type")
		};

		var transactionInfo = new TransactionInfo(destination, _walletModel.Settings.AnonScoreTarget)
		{
			Amount = amount,
			Recipient = label,
			PayJoinClient = GetPayjoinClient(PayJoinEndPoint),
			IsFixedAmount = IsFixedAmount,
			SubtractFee = amount == _parameters.AvailableCoins.TotalAmount() && !(IsFixedAmount || IsPayJoin)
		};

		if (_coinJoinManager is { } coinJoinManager)
		{
			await coinJoinManager.WalletEnteredSendingAsync(_wallet);
		}

		var sendParameters = _parameters with { TransactionInfo = transactionInfo };

		Navigate().To().TransactionPreview(_walletModel, sendParameters);
	}

	private async Task OnAutoPasteAsync()
	{
		var isAutoPasteEnabled = UiContext.ApplicationSettings.AutoPaste;

		if (string.IsNullOrEmpty(To) && isAutoPasteEnabled && IsNotInDonationWorkflow)
		{
			await OnPasteAsync(pasteIfInvalid: false);
		}
	}

	private async Task OnPasteAsync(bool pasteIfInvalid = true)
	{
		var text = await ApplicationHelper.GetTextAsync();

		lock (_parsingLock)
		{
			if (!TryParseUrl(text) && pasteIfInvalid)
			{
				To = text;
			}
		}
	}

	private IPayjoinClient? GetPayjoinClient(string? endPoint)
	{
		if (!string.IsNullOrWhiteSpace(endPoint) &&
			Uri.IsWellFormedUriString(endPoint, UriKind.Absolute))
		{
			var payjoinEndPointUri = new Uri(endPoint);
			if (Services.Config.UseTor != TorMode.Disabled)
			{
				if (payjoinEndPointUri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogWarning("Payjoin server is an onion service but Tor is disabled. Ignoring...");
					return null;
				}

				if (UiContext.ApplicationSettings.Network == Network.Main && payjoinEndPointUri.Scheme != Uri.UriSchemeHttps)
				{
					Logger.LogWarning("Payjoin server is not exposed as an onion service nor https. Ignoring...");
					return null;
				}
			}

			HttpClient httpClient = Services.HttpClientFactory.CreateClient(endPoint);
			httpClient.BaseAddress = new Uri(endPoint);
			return new PayjoinClient(payjoinEndPointUri, httpClient);
		}

		return null;
	}

	private async Task ShowQrCameraAsync()
	{
		var result = await Navigate().To().ShowQrCameraDialog(_walletModel.Network).GetResultAsync();
		if (!string.IsNullOrWhiteSpace(result))
		{
			To = result;
		}
	}

	private void ValidateAmount(IValidationErrors errors)
	{
		if (AmountBtc is null)
		{
			return;
		}

		if (AmountBtc > Constants.MaximumNumberOfBitcoins)
		{
			errors.Add(ErrorSeverity.Error, "Amount must be less than the total supply of BTC.");
		}
		else if (AmountBtc > _parameters.AvailableAmountBtc)
		{
			errors.Add(ErrorSeverity.Error, "Insufficient funds to cover the amount requested.");
		}
		else if (AmountBtc <= 0)
		{
			errors.Add(ErrorSeverity.Error, "Amount must be more than 0 BTC");
		}
		else if (_parsedAddress is Address.SilentPayment && AmountBtc < 0.00001m)
		{
			errors.Add(ErrorSeverity.Warning, "Most wallets don't recognize Silent Payments lower than 1000 sats.");
		}
	}

	private void ValidateToField(IValidationErrors errors)
	{
		if (!string.IsNullOrEmpty(To))
		{
			var parseResult = AddressParser.Parse(To, _walletModel.Network);
			if (!string.IsNullOrEmpty(To) && (To.IsTrimmable() || !parseResult.IsOk))
			{
				errors.Add(ErrorSeverity.Error, parseResult.Error);
				return;
			}
		}

		if (IsPayJoin && _walletModel.IsHardwareWallet)
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
		if (_parsingTo)
		{
			return false;
		}

		_parsingTo = true;

		text = text?.Trim();

		if (string.IsNullOrEmpty(text))
		{
			_parsingTo = false;
			PayJoinEndPoint = null;
			IsFixedAmount = false;
			return false;
		}

		// Reset PayJoinEndPoint by default
		PayJoinEndPoint = null;
		IsFixedAmount = false;

		var isSilentPayment = false;

		var result = AddressParser.Parse(text, _walletModel.Network)
			.Match(
				success =>
				{
					_parsedAddress = success;
					switch (success)
					{
						case Address.Bip21Uri bip21:
							To = bip21.Address.ToWif(_walletModel.Network);

							if (bip21.Amount is not null)
							{
								AmountBtc = bip21.Amount;
								IsFixedAmount = true;
							}

							if (!string.IsNullOrEmpty(bip21.Label))
							{
								SuggestionLabels = new SuggestionLabelsViewModel(
									_walletModel,
									Intent.Send,
									3,
									[bip21.Label]);
							}

							if (!string.IsNullOrEmpty(bip21.PayjoinEndpoint))
							{
								PayJoinEndPoint = bip21.PayjoinEndpoint;
							}
							return true;

						case Address.Bitcoin bitcoin:
							To = bitcoin.Address.ToString();
							return true;

						case Address.SilentPayment silentPayment:
							To = silentPayment.Address.ToWip(_walletModel.Network);
							isSilentPayment = true;
							return true;

						default:
							return true;
					}
				},
				_ => false);

		DisplaySilentPaymentInfo = isSilentPayment && _parameters.Donate;

		Dispatcher.UIThread.Post(() => _parsingTo = false);

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
				coinJoinManager.WalletEnteredSendWorkflow(_walletModel.Id);
			}
		}

		_suggestionLabels.Activate(disposables);

		RxApp.MainThreadScheduler.Schedule(async () => await OnAutoPasteAsync());

		base.OnNavigatedTo(inHistory, disposables);

		if (_parameters.Donate)
		{
			To = Constants.DonationAddress;
			Caption = "Donate to The Wasabi Wallet Developers to continue maintaining the software";
			IsFixedAddress = true;
			TryParseUrl(_to);
		}
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);

		if (!isInHistory && _coinJoinManager is { } coinJoinManager)
		{
			coinJoinManager.WalletLeftSendWorkflow(_wallet);
		}
	}
}
