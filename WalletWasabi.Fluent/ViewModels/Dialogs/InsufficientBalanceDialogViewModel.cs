using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Insufficient Balance")]
public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<bool>
{
	public InsufficientBalanceDialogViewModel(BalanceType type)
	{
		Question = type switch
		{
			BalanceType.Private => $"There are not enough private funds to cover the transaction fee. Instead of an extra cost, Wasabi can subtract the transaction fee from the amount.",
			BalanceType.Pocket => $"There are not enough funds selected to cover the transaction fee. Instead of an extra cost, Wasabi can subtract the transaction fee from the amount.",
			_ => $"There are not enough funds available to cover the transaction fee. Instead of an extra cost, Wasabi can subtract the transaction fee from the amount.",
		};

		Question += "\n\nWould you like to do this instead?";

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
	}

	public string Question { get; }
}
