using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Insufficient balance")]
	public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<bool>
	{
		public InsufficientBalanceDialogViewModel()
		{
			Text = $"You don't have enough private funds in your wallet including the fee. Instead of adding as an extra cost, Wasabi can subtract the fee from the maximum possible amount.\nWould you like Wasabi to do it?";

			NextCommand = ReactiveCommand.Create(() => Close(result: true));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
		}

		public string Text { get; }
	}
}
