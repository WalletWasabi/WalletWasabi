namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class EditableWorkflowStep : WorkflowStep
{
	public EditableWorkflowStep(
		bool requiresUserInput, 
		InputValidator userInputValidator, 
		bool skipStepFunc = false) 
		: base(requiresUserInput, userInputValidator, skipStepFunc)
	{
	}
}
