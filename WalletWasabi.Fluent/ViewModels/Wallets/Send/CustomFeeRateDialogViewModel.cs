using System.Globalization;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Advanced",
	NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CustomFeeRateDialogViewModel : DialogViewModelBase<FeeRate>
{
	private readonly TransactionInfo _transactionInfo;

	[AutoNotify] private string _customFee;

	public CustomFeeRateDialogViewModel(TransactionInfo transactionInfo)
	{
		_transactionInfo = transactionInfo;

		_customFee = transactionInfo.IsCustomFeeUsed
			? transactionInfo.FeeRate.SatoshiPerByte.ToString("0.00", CultureInfo.InvariantCulture)
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
			_transactionInfo.IsCustomFeeUsed = true;
			Close(DialogResultKind.Normal, new FeeRate(feeRate));
		}
		else
		{
			_transactionInfo.IsCustomFeeUsed = false;
			Close(DialogResultKind.Normal, FeeRate.Zero); // must return zero which indicates that it was cleared.
		}
	}

	private void ValidateCustomFee(IValidationErrors errors)
	{
		var customFeeString = CustomFee;

		if (customFeeString is "")
		{
			return;
		}

		if (!decimal.TryParse(customFeeString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
		{
			errors.Add(ErrorSeverity.Error, "The entered fee is not valid.");
			return;
		}

		if (value < Constants.MinRelayFeeRate.SatoshiPerByte)
		{
			errors.Add(ErrorSeverity.Error, $"Cannot be less than {Constants.MinRelayFeeRate.SatoshiPerByte} sat/vByte.");
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
