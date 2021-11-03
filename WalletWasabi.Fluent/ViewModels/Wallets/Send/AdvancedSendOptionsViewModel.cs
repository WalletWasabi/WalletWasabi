using System;
using System.Globalization;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Advanced")]
	public partial class AdvancedSendOptionsViewModel : RoutableViewModel
	{
		private readonly TransactionInfo _transactionInfo;

		[AutoNotify] private string _customFee;

		public AdvancedSendOptionsViewModel(TransactionInfo transactionInfo)
		{
			_transactionInfo = transactionInfo;

			_customFee = transactionInfo.CustomFeeRate != FeeRate.Zero
				? transactionInfo.CustomFeeRate.SatoshiPerByte.ToString(CultureInfo.InvariantCulture)
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
			_transactionInfo.CustomFeeRate =
				decimal.TryParse(CustomFee, NumberStyles.AllowDecimalPoint, new CultureInfo("en-US"), out var customFee)
					? new FeeRate(customFee)
					: FeeRate.Zero;

			Navigate().Back();
		}

		private void ValidateCustomFee(IValidationErrors errors)
		{
			var customFeeString = CustomFee;

			if (customFeeString is null or "")
			{
				return;
			}

			if (!decimal.TryParse(customFeeString, NumberStyles.AllowDecimalPoint, new CultureInfo("en-US"), out var value))
			{
				errors.Add(ErrorSeverity.Error, "The entered fee is not valid.");
				return;
			}

			if (value == decimal.Zero)
			{
				errors.Add(ErrorSeverity.Error, "Cannot be 0.");
				return;
			}

			try
			{
				_ = new FeeRate(value);
			}
			catch(OverflowException)
			{
				errors.Add(ErrorSeverity.Error, "The entered fee is too high.");
				return;
			}
		}
	}
}
