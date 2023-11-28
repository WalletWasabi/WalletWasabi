namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowStepViewModel
{
	[AutoNotify] private string? _message;
	[AutoNotify] private bool _requiresUserInput;
	[AutoNotify] private WorkflowInputValidatorViewModel? _userInputValidator;
	[AutoNotify] private bool _isCompleted;

	public WorkflowStepViewModel(
		string? message,
		bool requiresUserInput = false,
		WorkflowInputValidatorViewModel? userInputValidator = null)
	{
		_message = message;
		_requiresUserInput = requiresUserInput;
		_userInputValidator = userInputValidator;
	}
}
