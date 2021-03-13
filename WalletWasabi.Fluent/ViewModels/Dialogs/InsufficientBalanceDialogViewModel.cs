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
			var amount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var fee = transaction.Fee;

			switch (type)
			{
				case BalanceType.Private:
					Text = $"There are not enough private funds to cover the transaction fee. The closest Wasabi can do to your request is send {amount} BTC with a fee of {fee} BTC.\nWould you like to do that?";
					break;
				case BalanceType.Pocket:
					Text = $"There are not enough funds selected to cover the transaction fee. The closest Wasabi can do to your request is send {amount} BTC with a fee of {fee} BTC.\nWould you like to do that?";
					break;
				default:
					Text = $"There are not enough funds available to cover the transaction fee. The closest Wasabi can do to your request is send {amount} BTC with a fee of {fee} BTC.\nWould you like to do that?";
					break;
			}

			NextCommand = ReactiveCommand.Create(() => Close(result: true));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
		}

		public string Text { get; }
	}
}
