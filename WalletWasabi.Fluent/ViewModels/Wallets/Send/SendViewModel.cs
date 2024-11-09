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
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
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
using WalletWasabi.Userfacing.Bip21;
using WalletWasabi.Fluent.Models.Transactions;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	IconName = "wallet_action_send",
	Order = 5,
	Title = "SendViewModel_Title",
	Caption = "SendViewModel_Caption",
	Keywords = "SendViewModel_Keywords",
	Category = SearchCategory.Wallet,
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

	[AutoNotify] private string _to;
	[AutoNotify] private decimal? _amountBtc;
	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private bool _isFixedAmount;
	[AutoNotify] private bool _isPayJoin;
	[AutoNotify] private string? _payJoinEndPoint;
	[AutoNotify] private bool _conversionReversed;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private SuggestionLabelsViewModel _suggestionLabels;

	public SendViewModel(UiContext uiContext, IWalletModel walletModel, SendFlowModel parameters)
	{
		UiContext = uiContext;
		_to = "";

		_wallet = parameters.Wallet;
		_walletModel = walletModel;
		_parameters = parameters;
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		_conversionReversed = Services.UiConfig.SendAmountConversionReversed;

		ExchangeRate = _walletModel.AmountProvider.UsdExchangeRate;

		Balance =
			_parameters.IsManual
			? Observable.Return(_walletModel.AmountProvider.Create(_parameters.AvailableAmount))
			: _walletModel.Balances;

		_suggestionLabels = new SuggestionLabelsViewModel(_walletModel, Intent.Send, 3);

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
					var hasError = Validations.Any;

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

		var amount = new Money(amountBtc, MoneyUnit.BTC);
		var transactionInfo = new TransactionInfo(BitcoinAddress.Create(To, _walletModel.Network), _walletModel.Settings.AnonScoreTarget)
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

		if (string.IsNullOrEmpty(To) && isAutoPasteEnabled)
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
			errors.Add(ErrorSeverity.Error, Lang.Resources.SendViewModel_Error_Amount_LessThanSupply_Message);
		}
		else if (AmountBtc > _parameters.AvailableAmountBtc)
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.SendViewModel_Error_Amount_InsufficientFunds_Message);
		}
		else if (AmountBtc <= 0)
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.SendViewModel_Error_Amount_Negative_Message);
		}
	}

	private void ValidateToField(IValidationErrors errors)
	{
		if (!string.IsNullOrEmpty(To) && (To.IsTrimmable() || !AddressStringParser.TryParse(To, _walletModel.Network, out _)))
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.SendViewModel_Error_Address_Invalid_Message);
		}
		else if (IsPayJoin && _walletModel.IsHardwareWallet)
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.SendViewModel_Error_Payjoin_HardwareWalletNotPossible_Message);
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

		bool result = false;

		if (AddressStringParser.TryParse(text, _walletModel.Network, out Bip21UriParser.Result? parserResult))
		{
			result = true;

			PayJoinEndPoint = parserResult.UnknownParameters.TryGetValue("pj", out var endPoint) ? endPoint : null;

			if (parserResult.Address is { })
			{
				To = parserResult.Address.ToString();
			}

			if (parserResult.Amount is { })
			{
				AmountBtc = parserResult.Amount.ToDecimal(MoneyUnit.BTC);
				IsFixedAmount = true;
			}
			else
			{
				IsFixedAmount = false;
			}

			if (parserResult.Label is { } parsedLabel)
			{
				SuggestionLabels = new SuggestionLabelsViewModel(
				_walletModel,
				Intent.Send,
				3,
				[parsedLabel]);
			}
		}
		else
		{
			IsFixedAmount = false;
			PayJoinEndPoint = null;
		}

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

		_walletModel.AmountProvider.BtcToUsdExchangeRates
								   .BindTo(this, x => x.ExchangeRate)
								   .DisposeWith(disposables);

		RxApp.MainThreadScheduler.Schedule(async () => await OnAutoPasteAsync());

		base.OnNavigatedTo(inHistory, disposables);
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
