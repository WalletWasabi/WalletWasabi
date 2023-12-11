using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class SupportChatWorkflow : Workflow
{
	public SupportChatWorkflow(WorkflowState workflowState)
	{
		Steps = new List<WorkflowStep>
		{
			// User message
			new (true,
				new ChatMessageInputValidator(
					workflowState,
					"Send")),
			// TODO: Await the chat response from service?
		};

		CreateCanEditObservable();
	}
}
