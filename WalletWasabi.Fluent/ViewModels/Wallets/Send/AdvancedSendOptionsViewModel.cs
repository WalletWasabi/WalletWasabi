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

		[AutoNotify] private string _customFee = "";

		public AdvancedSendOptionsViewModel(TransactionInfo transactionInfo)
		{
			_transactionInfo = transactionInfo;
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
			if (decimal.TryParse(CustomFee, out var customFee))
			{
				_transactionInfo.CustomFeeRate = new FeeRate(customFee);
			}

			Navigate().Back();
		}

		private void ValidateCustomFee(IValidationErrors errors)
		{
			var customFeeString = CustomFee;

			if (!decimal.TryParse(customFeeString, out _))
			{
				errors.Add(ErrorSeverity.Error, "The entered fee is not valid");
			}
		}
	}
}
