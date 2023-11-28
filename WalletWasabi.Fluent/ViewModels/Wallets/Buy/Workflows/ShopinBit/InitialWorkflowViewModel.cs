using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class InitialWorkflowViewModel : WorkflowViewModel
{
	private const string AssistantName = "Clippy";

	public InitialWorkflowViewModel(string userName)
	{
		Steps = new List<WorkflowStepViewModel>
		{
			new (
				$"Hi {userName}, I am {AssistantName}, I can get you anything. Just tell me what you want, I will order it for you. Flights, cars..."),
			new (
				"First, tell me where do you want to order?"),
			new (
				null,
				requiresUserInput: true,
				userInputValidator: new LocationWorkflowInputValidatorViewModel()),
			new ("What do you need?"),
			new (null,
				requiresUserInput: true,
				userInputValidator: new RequestWorkflowInputValidatorViewModel()),
			new ("We have received you request, We will get back to you in a couple of days.")
		};
	}
}
