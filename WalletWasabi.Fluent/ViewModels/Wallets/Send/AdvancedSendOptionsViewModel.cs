using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Advanced")]
public partial class AdvancedSendOptionsViewModel : DialogViewModelBase<Unit>
{
	private readonly TransactionInfo _transactionInfo;

	[AutoNotify] private string _customFee;

	public AdvancedSendOptionsViewModel(TransactionInfo transactionInfo)
	{
		_transactionInfo = transactionInfo;

		_customFee = transactionInfo.IsCustomFeeUsed
			? transactionInfo.FeeRate.SatoshiPerByte.ToString(CultureInfo.InvariantCulture)
			: "";

		EnableBack = false;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.CustomFee, ValidateCustomFee);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.CustomFee)
				.Select(_ =>
				{
					var noError = !Validations.Any;
					var somethingFilled = CustomFee is not null or "";

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

		Close(DialogResultKind.Normal, Unit.Default);
	}

	private void ValidateCustomFee(IValidationErrors errors)
	{
		var customFeeString = CustomFee;

		if (customFeeString is null or "")
		{
			return;
		}

		if (customFeeString.Any(c => !char.IsDigit(c) && c != '.'))
		{
			errors.Add(ErrorSeverity.Error, "The field only accepts numbers.");
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
