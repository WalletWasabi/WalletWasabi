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
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Userfacing.Bip21;
using WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;
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
	private readonly CoinJoinManager? _coinJoinManager;

	private bool _parsingTo;
	private LabelsArray _parsedLabel = LabelsArray.Empty;

	[AutoNotify] private string _to;
	[AutoNotify] private Amount? _amount;
	[AutoNotify] private bool _isFixedAmount;
	[AutoNotify] private bool _isPayJoin;
	[AutoNotify] private string? _payJoinEndPoint;

	public SendViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		UiContext = uiContext;
		WalletVm = walletVm;

		CurrencyConversion = new CurrencyConversionViewModel(uiContext, walletVm.WalletModel);

		_to = "";
		_amount = Amount.Zero;

		_wallet = walletVm.Wallet;
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		Balance = walletVm.WalletModel.Balances;

		this.WhenAnyValue(x => x.CurrencyConversion.Amount)
			.BindTo(this, x => x.Amount);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		this.ValidateProperty(x => x.To, ValidateToField);
		this.ValidateProperty(x => x.Amount, ValidateAmount);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(ParseToField);

		this.WhenAnyValue(x => x.PayJoinEndPoint)
			.Subscribe(endPoint => IsPayJoin = endPoint is { });

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(async () => await OnAutoPasteAsync());
		InsertMaxCommand = ReactiveCommand.Create(() => CurrencyConversion.Amount = walletVm.WalletModel.AmountProvider.Create(_wallet.Coins.TotalAmount()));
		QrCommand = ReactiveCommand.Create(async () =>
		{
			ShowQrCameraDialogViewModel dialog = new(UiContext, _wallet.Network);
			var result = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);
			if (!string.IsNullOrWhiteSpace(result.Result))
			{
				To = result.Result;
			}
		});

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.Amount, x => x.To)
				.Select(tup =>
				{
					var (amount, to) = tup;
					var allFilled = !string.IsNullOrEmpty(to) && amount is { } a && a.Btc > Money.Zero;
					var hasError = Validations.Any;

					return allFilled && !hasError;
				});

		NextCommand = ReactiveCommand.CreateFromTask(
			async () =>
			{
				var labelDialog = new LabelEntryDialogViewModel(WalletVm.WalletModel, _parsedLabel);
				var result = await NavigateDialogAsync(labelDialog, NavigationTarget.CompactDialogScreen);
				if (result.Result is not { } label)
				{
					return;
				}

				var amount = Amount.Btc;
				var transactionInfo = new TransactionInfo(BitcoinAddress.Create(To, _wallet.Network), _wallet.AnonScoreTarget)
				{
					Amount = amount,
					Recipient = label,
					PayJoinClient = GetPayjoinClient(PayJoinEndPoint),
					IsFixedAmount = IsFixedAmount,
					SubtractFee = amount == _wallet.Coins.TotalAmount() && !(IsFixedAmount || IsPayJoin)
				};

				if (_coinJoinManager is { } coinJoinManager)
				{
					await coinJoinManager.WalletEnteredSendingAsync(_wallet);
				}

				Navigate().To().TransactionPreview(walletVm, transactionInfo);
			},
			nextCommandCanExecute);
	}

	public IObservable<Amount> Balance { get; }

	public CurrencyConversionViewModel CurrencyConversion { get; }

	public WalletViewModel WalletVm { get; }

	public bool IsQrButtonVisible => UiContext.QrCodeReader.IsPlatformSupported;

	public ICommand PasteCommand { get; }

	public ICommand AutoPasteCommand { get; }

	public ICommand QrCommand { get; }

	public ICommand InsertMaxCommand { get; }

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
		if (ApplicationHelper.Clipboard is { } clipboard)
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

	private IPayjoinClient? GetPayjoinClient(string? endPoint)
	{
		if (!string.IsNullOrWhiteSpace(endPoint) &&
			Uri.IsWellFormedUriString(endPoint, UriKind.Absolute))
		{
			var payjoinEndPointUri = new Uri(endPoint);
			if (!Services.PersistentConfig.UseTor)
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

			IHttpClient httpClient = Services.HttpClientFactory.NewHttpClient(() => payjoinEndPointUri, Mode.DefaultCircuit);
			return new PayjoinClient(payjoinEndPointUri, httpClient);
		}

		return null;
	}

	private void ValidateAmount(IValidationErrors errors)
	{
		if (Amount is null)
		{
			errors.Add(ErrorSeverity.Error, "Invalid Amount.");
			return;
		}

		if (Amount.BtcValue > Constants.MaximumNumberOfBitcoins)
		{
			errors.Add(ErrorSeverity.Error, "Amount must be less than the total supply of BTC.");
		}
		else if (Amount.BtcValue > _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC))
		{
			errors.Add(ErrorSeverity.Error, "Insufficient funds to cover the amount requested.");
		}
		else if (Amount.BtcValue <= 0m)
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

		if (AddressStringParser.TryParse(text, _wallet.Network, out Bip21UriParser.Result? parserResult))
		{
			result = true;

			_parsedLabel = parserResult.Label is { } label ? new LabelsArray(label) : LabelsArray.Empty;

			PayJoinEndPoint = parserResult.UnknownParameters.TryGetValue("pj", out var endPoint) ? endPoint : null;

			if (parserResult.Address is { })
			{
				To = parserResult.Address.ToString();
			}

			if (parserResult.Amount is { })
			{
				Amount = WalletVm.WalletModel.AmountProvider.Create(parserResult.Amount);
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
			_parsedLabel = LabelsArray.Empty;
		}

		Dispatcher.UIThread.Post(() => _parsingTo = false);

		return result;
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		if (!inHistory)
		{
			To = "";
			Amount = Amount.Zero;
			ClearValidations();

			if (_coinJoinManager is { } coinJoinManager)
			{
				coinJoinManager.WalletEnteredSendWorkflow(_wallet.WalletName);
			}
		}

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
