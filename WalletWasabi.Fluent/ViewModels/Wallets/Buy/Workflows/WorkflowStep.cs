using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowStep : ReactiveObject
{
	[AutoNotify] private bool _requiresUserInput;
	[AutoNotify] private InputValidator _userInputValidator;
	[AutoNotify] private bool _isCompleted;

	private readonly bool _skipStepFunc;

	public WorkflowStep(
		bool requiresUserInput,
		InputValidator userInputValidator,
		bool skipStepFunc = false)
	{
		_requiresUserInput = requiresUserInput;
		_userInputValidator = userInputValidator;
		_skipStepFunc = skipStepFunc;
	}

	public bool SkipStep() => _skipStepFunc;

	public virtual void Update(string message)
	{
		// TODO: Add EditableWorkflowStep class
	}
}
