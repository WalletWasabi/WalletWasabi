using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class InitialWorkflowViewModel : WorkflowViewModel
{
	private const string AssistantName = "Clippy";

	private readonly InitialWorkflowRequest _request;

	public InitialWorkflowViewModel(IWorkflowValidator workflowValidator, string userName)
	{
		_request = new InitialWorkflowRequest();

		Steps = new List<WorkflowStepViewModel>
		{
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"Hi {userName}, I am {AssistantName}, I can get you anything. Just tell me what you want, I will order it for you. Flights, cars...")),
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"First, tell me where do you want to order?")),
			new (requiresUserInput: true,
				userInputValidator: new LocationWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"What do you need?")),
			new (requiresUserInput: true,
				userInputValidator: new RequestWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			new (false,
				new NoInputWorkflowInputValidatorViewModel(
					workflowValidator,
					"We have received you request, We will get back to you in a couple of days."))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
