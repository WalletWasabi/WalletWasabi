using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Advanced",
	NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CustomFeeRateDialogViewModel : DialogViewModelBase<FeeRate>
{
	private readonly TransactionInfo _transactionInfo;

	public CustomFeeRateDialogViewModel(UiContext uiContext, TransactionInfo transactionInfo)
	{
		_transactionInfo = transactionInfo;

		UiContext = uiContext;

		EnableBack = false;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		CustomFeeViewModel = new CurrencyInputViewModel(UiContext, CurrencyFormat.SatsvByte);
		CustomFeeViewModel.ValidateProperty(x => x.Value, ValidateCustomFee);

		CustomFeeViewModel.SetValue(transactionInfo.CustomFee);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.CustomFeeViewModel.Value).Select(_ => CustomFeeViewModel.IsValid());

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);
	}

	public CurrencyInputViewModel CustomFeeViewModel { get; }

	private void OnNext()
	{
		if (CustomFeeViewModel.Value is CurrencyValue.Valid feeRate)
		{
			_transactionInfo.IsCustomFeeUsed = true;
			Close(DialogResultKind.Normal, new FeeRate(feeRate.Value));
		}
		else
		{
			_transactionInfo.IsCustomFeeUsed = false;
			Close(DialogResultKind.Normal, FeeRate.Zero); // must return zero which indicates that it was cleared.
		}
	}

	private void ValidateCustomFee(IValidationErrors errors)
	{
		if (CustomFeeViewModel.Value is CurrencyValue.Empty)
		{
			return;
		}
		else if (CustomFeeViewModel.Value is CurrencyValue.Invalid)
		{
			errors.Add(ErrorSeverity.Error, "Please enter a valid value.");
		}
		else if (CustomFeeViewModel.Value is CurrencyValue.Valid v)
		{
			var value = v.Value;

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
}
