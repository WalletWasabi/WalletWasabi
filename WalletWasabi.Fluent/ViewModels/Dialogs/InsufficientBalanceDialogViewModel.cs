using ReactiveUI;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Insufficient Balance")]
	public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<bool>
	{
		public InsufficientBalanceDialogViewModel(BalanceType type)
		{
			switch (type)
			{
				case BalanceType.Private:
					Text = $"With the included fee, you don't have enough private funds in your wallet. Instead of adding as an extra cost, Wasabi can subtract the fee from the maximum possible amount.\nWould you like Wasabi to do it?";
					break;
				case BalanceType.Pocket:
					Text = $"With the included fee, you don't have enough funds in the selected pocket. Instead of adding as an extra cost, Wasabi can subtract the fee from the amount.\nWould you like Wasabi to do it?";
					break;
				default:
					Text = $"With the included fee, you don't have enough funds in your wallet. Instead of adding as an extra cost, Wasabi can subtract the fee from the amount.\nWould you like Wasabi to do it?";
					break;
			}

			NextCommand = ReactiveCommand.Create(() => Close(result: true));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
		}

		public string Text { get; }
	}
}
