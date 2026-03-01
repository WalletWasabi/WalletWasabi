using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
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
	private readonly Lock _parsingLock = new();
	private readonly Wallet _wallet;
	private readonly IWalletModel _walletModel;
	private readonly SendFlowModel _parameters;
	private readonly CoinJoinManager? _coinJoinManager;
	private readonly ObservableAsPropertyHelper<Amount?> _balanceLatest;

	private bool _parsingTo;
	private Address? _parsedAddress;

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
	[AutoNotify] private string? _usdContent;
	[AutoNotify] private string? _bitcoinContent;
	[AutoNotify] private bool _isPayToMany;
	[AutoNotify] private string _addressWatermark = DefaultAddressWatermark;

	private const string DefaultAddressWatermark = "(e.g. Bitcoin address, Silent Payment address or payjoin URL)";
	private const string PayToManyAddressWatermark = "(e.g. Bitcoin address or Silent Payment address)";

	// Tracks which recipient (if any) has Max selected.
	// null = none, 0 = primary, 1+ = index into AdditionalRecipients + 1.
	private int? _maxRecipientIndex;
	private bool _settingMaxAmount;
	private readonly Subject<Unit> _recipientsChanged = new();

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

		this.WhenAnyValue(x => x.Balance)
			.Switch()
			.ToProperty(this, vm => vm.BalanceLatest, out _balanceLatest);

		this.WhenAnyValue(x => x.AmountBtc)
			.Subscribe(_ =>
			{
				if (!_settingMaxAmount && _maxRecipientIndex == 0)
				{
					_maxRecipientIndex = null;
				}
			});

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(OnAutoPasteAsync);
		InsertMaxCommand = ReactiveCommand.Create(() => SetMaxForRecipient(0));
		QrCommand = ReactiveCommand.Create(ShowQrCameraAsync);

		AdditionalRecipients = new ObservableCollection<RecipientRowViewModel>();
		AddRecipientCommand = ReactiveCommand.Create(OnAddRecipient);

		this.WhenAnyValue(x => x.IsPayToMany)
			.Skip(1)
			.Subscribe(isPayToMany =>
			{
				AddressWatermark = isPayToMany ? PayToManyAddressWatermark : DefaultAddressWatermark;
				if (isPayToMany)
				{
					PayJoinEndPoint = null;
				}
			});

		AdditionalRecipients.CollectionChanged += (_, _) =>
		{
			IsPayToMany = AdditionalRecipients.Count > 0;
			ReindexRecipients();
			_recipientsChanged.OnNext(Unit.Default);
		};

		var primaryChanged = this.WhenAnyValue(
				x => x.AmountBtc,
				x => x.To,
				x => x.SuggestionLabels.Labels.Count,
				x => x.SuggestionLabels.IsCurrentTextValid)
			.Select(_ => Unit.Default);

		var nextCommandCanExecute = primaryChanged
			.Merge(_recipientsChanged)
			.Select(_ =>
			{
				var allFilled = !string.IsNullOrEmpty(To) && AmountBtc > 0;
				var hasError = Validations.AnyErrors;
				var labelsCount = SuggestionLabels.Labels.Count;
				var isCurrentTextValid = SuggestionLabels.IsCurrentTextValid;

				if (allFilled && AdditionalRecipients.Count > 0)
				{
					ValidateAdditionalRecipientBalances();
					allFilled = AdditionalRecipients.All(r => r.IsValid);
				}

				return allFilled && !hasError && (labelsCount > 0 || isCurrentTextValid);
			});

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);
		PasteFromClipboardCommand = ReactiveCommand.CreateFromTask<object>(PasteFromClipboardAsync);

		this.WhenAnyValue(x => x.ConversionReversed)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.SendAmountConversionReversed = x);
	}

	public IObservable<Amount> Balance { get; }

	public Amount? BalanceLatest => _balanceLatest.Value;

	public bool IsQrButtonVisible => UiContext.QrCodeReader.IsPlatformSupported;

	public bool IsNotInDonationWorkflow => !_parameters.Donate;

	public ICommand PasteCommand { get; }

	public ICommand AutoPasteCommand { get; }

	public ICommand QrCommand { get; }

	public ICommand InsertMaxCommand { get; }

	public ICommand? PasteFromClipboardCommand { get; }

	public ObservableCollection<RecipientRowViewModel> AdditionalRecipients { get; }

	public ICommand AddRecipientCommand { get; }

	private void SetMaxForRecipient(int recipientIndex)
	{
		_maxRecipientIndex = recipientIndex;
		_settingMaxAmount = true;

		if (recipientIndex == 0)
		{
			// Primary recipient: Max = total available minus all additional amounts.
			var otherAmounts = AdditionalRecipients.Where(r => r.AmountBtc.HasValue).Sum(r => r.AmountBtc!.Value);
			AmountBtc = Math.Max(0m, _parameters.AvailableAmountBtc - otherAmounts);
		}
		else
		{
			// Additional recipient: Max = remaining balance for that row.
			var row = AdditionalRecipients[recipientIndex - 1];
			row.AmountBtc = Math.Max(0m, GetRemainingBalanceFor(row));
		}

		_settingMaxAmount = false;
	}

	private decimal GetRemainingBalanceFor(RecipientRowViewModel excludeRow)
	{
		var primaryAmount = AmountBtc ?? 0m;
		var otherAdditionalAmounts = AdditionalRecipients
			.Where(r => r != excludeRow && r.AmountBtc.HasValue)
			.Sum(r => r.AmountBtc!.Value);
		var remaining = _parameters.AvailableAmountBtc - primaryAmount - otherAdditionalAmounts;
		return Math.Max(0m, remaining);
	}

	private void ValidateAdditionalRecipientBalances()
	{
		var totalAmountBtc = (AmountBtc ?? 0m) + AdditionalRecipients
			.Where(r => r.AmountBtc.HasValue)
			.Sum(r => r.AmountBtc!.Value);

		var overBudget = totalAmountBtc > _parameters.AvailableAmountBtc;

		foreach (var row in AdditionalRecipients)
		{
			row.AmountError = overBudget && row.AmountBtc > 0
				? "Insufficient funds to cover the total amount requested."
				: null;
		}
	}

	private void OnAddRecipient()
	{
		var row = new RecipientRowViewModel(
			_walletModel,
			_walletModel.Network,
			onRemove: r =>
			{
				AdditionalRecipients.Remove(r);
				r.Dispose();
			},
			onInsertMax: r =>
			{
				int index = AdditionalRecipients.IndexOf(r) + 1;
				SetMaxForRecipient(index);
			},
			scanQrCodeAsync: async () => await Navigate().To().ShowQrCameraDialog(_walletModel.Network).GetResultAsync(),
			isQrButtonVisible: IsQrButtonVisible);

		row.WhenAnyValue(r => r.AmountBtc, r => r.To, r => r.SuggestionLabels.Labels.Count, r => r.SuggestionLabels.IsCurrentTextValid)
			.Subscribe(_ =>
			{
				// Clear max if user manually edits this row's amount.
				if (!_settingMaxAmount)
				{
					int rowIndex = AdditionalRecipients.IndexOf(row) + 1;
					if (_maxRecipientIndex == rowIndex)
					{
						_maxRecipientIndex = null;
					}
				}
				_recipientsChanged.OnNext(Unit.Default);
			});

		AdditionalRecipients.Add(row);
	}

	private void ReindexRecipients()
	{
		for (int i = 0; i < AdditionalRecipients.Count; i++)
		{
			AdditionalRecipients[i].Index = i + 2;
		}
	}

	private static Destination AddressToDestination(Address parsedAddress)
	{
		return parsedAddress switch
		{
			Address.Bitcoin bitcoin => new Destination.Loudly(bitcoin.Address.ScriptPubKey),
			Address.Bip21Uri { Address: Address.Bitcoin bitcoin } => new Destination.Loudly(bitcoin.Address.ScriptPubKey),
			Address.Bip21Uri { Address: Address.SilentPayment silentPayment } => new Destination.Silent(silentPayment.Address),
			Address.SilentPayment silentPayment => new Destination.Silent(silentPayment.Address),
			_ => throw new ArgumentException("Unknown address type")
		};
	}

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
		Destination destination = AddressToDestination(parsedAddress);

		var additionalRecipients = AdditionalRecipients
			.Where(r => r.IsValid && r.ParsedAddress is not null)
			.Select((r, index) => new RecipientInfo(
				AddressToDestination(r.ParsedAddress!),
				new Money(r.AmountBtc!.Value, MoneyUnit.BTC),
				new LabelsArray(r.SuggestionLabels.Labels.ToArray()),
				IsSubtractFee: _maxRecipientIndex == index + 1))
			.ToList();

		var isPayToMany = additionalRecipients.Count > 0;

		var primarySubtractFee = isPayToMany
			? _maxRecipientIndex == 0
			: amount == _parameters.AvailableCoins.TotalAmount() && !(IsFixedAmount || IsPayJoin);

		var transactionInfo = new TransactionInfo(destination, _walletModel.Settings.AnonScoreTarget)
		{
			Amount = amount,
			Recipient = label,
			PayJoinClient = isPayToMany ? null : GetPayjoinClient(PayJoinEndPoint),
			IsFixedAmount = IsFixedAmount,
			SubtractFee = primarySubtractFee,
			AdditionalRecipients = additionalRecipients
		};

		if (_coinJoinManager is { } coinJoinManager)
		{
			await coinJoinManager.WalletEnteredSendingAsync(_wallet);
		}

		var sendParameters = _parameters with { TransactionInfo = transactionInfo };

		Navigate().To().TransactionPreview(_walletModel, sendParameters);
	}

	private async Task PasteFromClipboardAsync(object? parameter)
	{
		if (parameter is not DualCurrencyEntryBox box)
		{
			return;
		}

		string content = await ApplicationHelper.GetTextAsync();

		if (box.IsFiat)
		{
			var usd = ClipboardObserver.ParseToUsd(content);
			if (usd is not null)
			{
				UsdContent = usd.Value.ToString("0.00");
			}
		}
		else
		{
			var latestBalance = BalanceLatest;
			if (latestBalance is not null)
			{
				var btc = ClipboardObserver.ParseToMoney(content, latestBalance.Btc);
				if (btc is not null)
				{
					BitcoinContent = btc;
				}
			}
		}
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
		else if (AmountBtc <= 0)
		{
			errors.Add(ErrorSeverity.Error, "Amount must be more than 0 BTC");
		}
		else
		{
			var totalAmountBtc = AmountBtc.Value + AdditionalRecipients.Where(r => r.AmountBtc.HasValue).Sum(r => r.AmountBtc!.Value);
			if (totalAmountBtc > _parameters.AvailableAmountBtc)
			{
				errors.Add(ErrorSeverity.Error, "Insufficient funds to cover the total amount requested.");
			}
		}

		if (_parsedAddress is Address.SilentPayment && AmountBtc < 0.00001m)
		{
			errors.Add(ErrorSeverity.Warning, "Most wallets don't recognize Silent Payments lower than 1000 sats.");
		}
	}

	private void ValidateToField(IValidationErrors errors)
	{
		var parseResult = AddressParser.Parse(To, _walletModel.Network);
		if (!parseResult.IsOk)
		{
			errors.Add(ErrorSeverity.Error, parseResult.Error);
			return;
		}
		if (parseResult is {Value: Address.SilentPayment} && _walletModel.IsHardwareWallet)
		{
			errors.Add(ErrorSeverity.Error, "Silent payments are not possible with hardware wallets.");
			return;
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
			foreach (var r in AdditionalRecipients)
			{
				r.Dispose();
			}
			AdditionalRecipients.Clear();
			_maxRecipientIndex = null;
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
