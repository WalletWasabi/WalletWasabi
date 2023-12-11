using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Dialogs;

[NavigationMetaData(Title = "Edit Message", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class EditMessageDialogViewModel : DialogViewModelBase<string?>
{
	[AutoNotify] private InputValidator? _userInputValidator;

	public EditMessageDialogViewModel(InputValidator userInputValidator, WorkflowState workflowState)
	{
		UserInputValidator = userInputValidator;

		UserInputValidator.OnActivation();

		workflowState.SignalValid(true);

		NextCommand = ReactiveCommand.Create(() => Close(result: UserInputValidator.GetFinalMessage()), workflowState.IsValidObservable);
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel, result: null));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}
}
