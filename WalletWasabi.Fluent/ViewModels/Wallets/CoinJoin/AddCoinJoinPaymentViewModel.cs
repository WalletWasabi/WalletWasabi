using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;
using Address = WalletWasabi.Userfacing.Address;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Fluent.ViewModels.Wallets.CoinJoin;

[NavigationMetaData(
	Title = "Add Coinjoin Payment",
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class AddCoinJoinPaymentViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	private readonly IWalletModel _walletModel;
	private Address? _parsedAddress;

	[AutoNotify] private string _to = "";
	[AutoNotify] private decimal? _amountBtc;
	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private bool _conversionReversed;

	private AddCoinJoinPaymentViewModel(IWalletModel walletModel, Wallet wallet)
	{
		_wallet = wallet;
		_walletModel = walletModel;

		_exchangeRate = Services.Status.UsdExchangeRate;
		_conversionReversed = Services.UiConfig.SendAmountConversionReversed;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = true;

		this.ValidateProperty(x => x.To, ValidateToField);
		this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(ParseToField);

		var canExecute = this.WhenAnyValue(
				x => x.AmountBtc,
				x => x.To)
			.Select(_ => !string.IsNullOrWhiteSpace(To) && AmountBtc > 0 && !Validations.AnyErrors);

		NextCommand = ReactiveCommand.CreateFromTask(OnAddPaymentAsync, canExecute);

		PasteCommand = ReactiveCommand.CreateFromTask(OnPasteAsync);
	}

	public ICommand PasteCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!isInHistory)
		{
			To = "";
			AmountBtc = null;
			ClearValidations();
		}
	}

	private async Task OnPasteAsync()
	{
		var text = await ApplicationHelper.GetTextAsync();
		if (!string.IsNullOrWhiteSpace(text))
		{
			To = text.Trim();
		}
	}

	private void ParseToField(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			_parsedAddress = null;
			return;
		}

		var result = AddressParser.Parse(text.Trim(), _walletModel.Network);
		if (result.IsOk)
		{
			_parsedAddress = result.Value;

			// If it's a BIP21 URI with an amount, fill in the amount
			if (_parsedAddress is Address.Bip21Uri bip21 && bip21.Amount.HasValue)
			{
				AmountBtc = bip21.Amount.Value;
			}
		}
		else
		{
			_parsedAddress = null;
		}
	}

	private void ValidateToField(IValidationErrors errors)
	{
		if (string.IsNullOrWhiteSpace(To))
		{
			return;
		}

		var parseResult = AddressParser.Parse(To.Trim(), _walletModel.Network);
		if (!parseResult.IsOk)
		{
			errors.Add(ErrorSeverity.Error, parseResult.Error);
			return;
		}

		// Silent payments are not supported for CoinJoin payments
		if (parseResult.Value is Address.SilentPayment)
		{
			errors.Add(ErrorSeverity.Error, "Silent payments are not supported for Coinjoin payments.");
			return;
		}

		if (parseResult.Value is Address.Bip21Uri { Address: Address.SilentPayment })
		{
			errors.Add(ErrorSeverity.Error, "Silent payments are not supported for Coinjoin payments.");
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
			errors.Add(ErrorSeverity.Error, "Amount must be more than 0 BTC.");
		}
	}

	private async Task OnAddPaymentAsync()
	{
		if (AmountBtc is not { } amountBtc || _parsedAddress is null)
		{
			return;
		}

		var amount = new Money(amountBtc, MoneyUnit.BTC);
		IDestination? destination = _parsedAddress switch
		{
			Address.Bitcoin bitcoin => bitcoin.Address,
			Address.Bip21Uri { Address: Address.Bitcoin bitcoin } => bitcoin.Address,
			_ => null
		};

		if (destination is null)
		{
			return;
		}

		try
		{
			_wallet.BatchedPayments.AddPayment(destination, amount);
			Navigate().Back();
		}
		catch (Exception ex)
		{
			await ShowErrorAsync("Error", ex.Message, "Failed to add payment");
		}
	}
}
