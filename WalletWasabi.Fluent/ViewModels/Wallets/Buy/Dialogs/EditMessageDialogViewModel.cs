using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Dialogs;

[NavigationMetaData(Title = "Edit Message", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class EditMessageDialogViewModel : DialogViewModelBase<ChatMessage>
{
	public EditMessageDialogViewModel(IWorkflowStep editor)
	{
		NextCommand = editor.SendCommand;
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel, result: null));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}
}
