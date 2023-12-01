using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowStep : ReactiveObject
{
	[AutoNotify] private bool _requiresUserInput;
	[AutoNotify] private InputValidator _userInputValidator;
	[AutoNotify] private bool _isCompleted;

	public WorkflowStep(
		bool requiresUserInput,
		InputValidator userInputValidator)
	{
		_requiresUserInput = requiresUserInput;
		_userInputValidator = userInputValidator;
	}
}
