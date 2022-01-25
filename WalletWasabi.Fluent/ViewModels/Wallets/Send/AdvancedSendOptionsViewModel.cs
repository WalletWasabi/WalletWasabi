using System;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Advanced")]
public partial class AdvancedSendOptionsViewModel : DialogViewModelBase<Unit>
{
	private readonly TransactionInfo _transactionInfo;
	private readonly string _destinationAddressString;

	[AutoNotify] private string _customFee;
	[AutoNotify] private string _customChangeAddress;

	public AdvancedSendOptionsViewModel(TransactionInfo transactionInfo, string destinationAddressString)
	{
		_transactionInfo = transactionInfo;
		_destinationAddressString = destinationAddressString;

		_customFee = transactionInfo.IsCustomFeeUsed
			? transactionInfo.FeeRate.SatoshiPerByte.ToString(CultureInfo.InvariantCulture)
			: "";

		_customChangeAddress = transactionInfo.CustomChangeAddress is { }
			? transactionInfo.CustomChangeAddress.ToString()
			: "";

		EnableBack = false;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.CustomFee, ValidateCustomFee);
		this.ValidateProperty(x => x.CustomChangeAddress, ValidateCustomChangeAddress);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.CustomFee, x => x.CustomChangeAddress)
				.Select(_ =>
				{
					var noError = !Validations.Any;
					var somethingFilled = CustomFee is not null or "" || CustomChangeAddress is not null or "";

					return noError && somethingFilled;
				});

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);
	}

	private void OnNext()
	{
		if (decimal.TryParse(CustomFee, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var feeRate))
		{
			_transactionInfo.FeeRate = new FeeRate(feeRate);
			_transactionInfo.IsCustomFeeUsed = true;
		}
		else if (_transactionInfo.IsCustomFeeUsed)
		{
			_transactionInfo.FeeRate = FeeRate.Zero;
			_transactionInfo.IsCustomFeeUsed = false;
		}

		_transactionInfo.CustomChangeAddress =
			AddressStringParser.TryParse(CustomChangeAddress, Services.WalletManager.Network, out var bitcoinUrlBuilder)
				? bitcoinUrlBuilder.Address
				: null;

		Close(DialogResultKind.Normal, Unit.Default);
	}

	private void ValidateCustomChangeAddress(IValidationErrors errors)
	{
		var address = CustomChangeAddress;

		if (address is null or "")
		{
			return;
		}

		if (!AddressStringParser.TryParse(address, Services.WalletManager.Network, out _))
		{
			errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
			return;
		}

		if (address == _destinationAddressString)
		{
			errors.Add(ErrorSeverity.Error, "Cannot be the same as the destination address.");
			return;
		}
	}

	private void ValidateCustomFee(IValidationErrors errors)
	{
		var customFeeString = CustomFee;

		if (customFeeString is null or "")
		{
			return;
		}

		if (!decimal.TryParse(customFeeString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
		{
			errors.Add(ErrorSeverity.Error, "The entered fee is not valid.");
			return;
		}

		if (value < decimal.One)
		{
			errors.Add(ErrorSeverity.Error, "Cannot be less than 1 sat/vByte.");
			return;
		}

		try
		{
			_ = new FeeRate(value);
		}
		catch (OverflowException)
		{
			errors.Add(ErrorSeverity.Error, "The entered fee is too high.");
			return;
		}
	}
}
