using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowStep : ReactiveObject
{
	[AutoNotify] private bool _requiresUserInput;
	[AutoNotify] private InputValidator _userInputValidator;
	[AutoNotify] private bool _isCompleted;

	private readonly Func<bool>? _skipStepFunc;

	public WorkflowStep(
		bool requiresUserInput,
		InputValidator userInputValidator,
		Func<bool>? skipStepFunc = null)
	{
		_requiresUserInput = requiresUserInput;
		_userInputValidator = userInputValidator;
		_skipStepFunc = skipStepFunc;
	}

	public bool SkipStep() => _skipStepFunc is not null && _skipStepFunc();
}
