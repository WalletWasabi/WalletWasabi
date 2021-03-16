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
		public InsufficientBalanceDialogViewModel(BalanceType type, BuildTransactionResult transaction)
		{
			AmountBtc = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			// AmountFee = transaction.Fee;

			switch (type)
			{
				case BalanceType.Private:
					Caption = $"There are not enough private funds to cover the transaction fee. The closest Wasabi can do to your request is send:";
					break;
				case BalanceType.Pocket:
					Caption = $"There are not enough funds selected to cover the transaction fee. The closest Wasabi can do to your request is send:";
					break;
				default:
					Caption = $"There are not enough funds available to cover the transaction fee. The closest Wasabi can do to your request is send:";
					break;
			}

			NextCommand = ReactiveCommand.Create(() => Close(result: true));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
		}

		public string Caption { get; }

		public decimal AmountBtc { get; }

		public decimal AmountFee { get; }
	}
}
