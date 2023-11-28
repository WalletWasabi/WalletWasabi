namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowStepViewModel
{
	[AutoNotify] private bool _requiresUserInput;
	[AutoNotify] private WorkflowInputValidatorViewModel _userInputValidator;
	[AutoNotify] private bool _isCompleted;

	public WorkflowStepViewModel(
		bool requiresUserInput,
		WorkflowInputValidatorViewModel userInputValidator)
	{
		_requiresUserInput = requiresUserInput;
		_userInputValidator = userInputValidator;
	}
}
