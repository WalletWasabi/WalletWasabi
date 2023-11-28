using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class InitialWorkflowViewModel : WorkflowViewModel
{
	private const string AssistantName = "Clippy";

	public InitialWorkflowViewModel(string userName)
	{
		Steps = new List<WorkflowStepViewModel>
		{
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					$"Hi {userName}, I am {AssistantName}, I can get you anything. Just tell me what you want, I will order it for you. Flights, cars...")),
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					"First, tell me where do you want to order?")),
			new (requiresUserInput: true,
				userInputValidator: new LocationWorkflowInputValidatorViewModel()),
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					"What do you need?")),
			new (requiresUserInput: true,
				userInputValidator: new RequestWorkflowInputValidatorViewModel()),
			new (false,
				new NoInputWorkflowInputValidatorViewModel(
					"We have received you request, We will get back to you in a couple of days."))
		};
	}
}
