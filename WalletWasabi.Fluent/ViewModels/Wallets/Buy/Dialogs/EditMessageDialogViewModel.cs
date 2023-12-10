using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Dialogs;

[NavigationMetaData(Title = "Edit Message", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class EditMessageDialogViewModel : DialogViewModelBase<bool>
{
	public EditMessageDialogViewModel(string message)
	{
		Message = message;

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string Message { get; }
}
