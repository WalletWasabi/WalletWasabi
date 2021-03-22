using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Insufficient Balance")]
	public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<bool>
	{
		public InsufficientBalanceDialogViewModel(BalanceType type, BuildTransactionResult transaction, decimal usdExchangeRate)
		{
			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var fee = transaction.Fee;

			BtcAmountText = $"{destinationAmount} bitcoins ";
			FiatAmountText = $"(≈{(destinationAmount * usdExchangeRate).FormattedFiat()} USD) ";

			BtcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} satoshis ";
			FiatFeeText = $"(≈{(fee.ToDecimal(MoneyUnit.BTC) * usdExchangeRate).FormattedFiat()} USD)";

			switch (type)
			{
				case BalanceType.Private:
					Caption = $"There are not enough private funds to cover the transaction fee. Alternatively you could:";
					break;
				case BalanceType.Pocket:
					Caption = $"There are not enough funds selected to cover the transaction fee. Alternatively you could:";
					break;
				default:
					Caption = $"There are not enough funds available to cover the transaction fee. Alternatively you could:";
					break;
			}

			NextCommand = ReactiveCommand.Create(() => Close(result: true));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
		}

		public string FiatFeeText { get; set; }

		public string BtcFeeText { get; set; }

		public string FiatAmountText { get; set; }

		public string BtcAmountText { get; set; }

		public string Caption { get; }
	}
}
