namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class EditableWorkflowStep : WorkflowStep
{
	private readonly Action<string>? _updateRequest;

	public EditableWorkflowStep(
		bool requiresUserInput,
		InputValidator userInputValidator,
		Action<string>? updateRequest,
		bool skipStepFunc = false)
		: base(requiresUserInput, userInputValidator, skipStepFunc)
	{
		_updateRequest = updateRequest;
	}

	public override void Update(string message)
	{
		base.Update(message);
		_updateRequest?.Invoke(message);
	}
}
