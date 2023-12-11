using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Dialogs;

[NavigationMetaData(Title = "Edit Message", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class EditMessageDialogViewModel : DialogViewModelBase<string?>
{
	public EditMessageDialogViewModel(string message)
	{
		Message = message;

		NextCommand = ReactiveCommand.Create(() => Close(result: Message));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel, result: null));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string Message { get; }
}
