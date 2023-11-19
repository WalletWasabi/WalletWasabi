using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Advanced",
	NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CustomFeeRateDialogViewModel : DialogViewModelBase<FeeRate>
{
	private readonly TransactionInfo _transactionInfo;

	[AutoNotify] private decimal? _customFee;

	public CustomFeeRateDialogViewModel(TransactionInfo transactionInfo)
	{
		_transactionInfo = transactionInfo;

		_customFee =
			transactionInfo.IsCustomFeeUsed
			? transactionInfo.FeeRate.SatoshiPerByte
			: null;

		EnableBack = false;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.CustomFee, ValidateCustomFee);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.CustomFee)
				.Select(_ =>
				{
					var noError = !Validations.Any;
					var somethingFilled = CustomFee is not null;

					return noError && somethingFilled;
				});

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);
	}

	private void OnNext()
	{
		if (CustomFee is { } feeRate)
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
		if (CustomFee is not { } value)
		{
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
