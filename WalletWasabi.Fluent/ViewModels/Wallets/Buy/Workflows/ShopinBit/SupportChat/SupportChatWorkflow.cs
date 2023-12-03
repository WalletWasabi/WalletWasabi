using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class SupportChatWorkflow : Workflow
{
	private readonly SupportChatWorkflowRequest _request;

	public SupportChatWorkflow(IWorkflowValidator workflowValidator)
	{
		_request = new SupportChatWorkflowRequest();

		Steps = new List<WorkflowStep>
		{
			// User message
			new (true,
				new ChatMessageInputValidator(
					workflowValidator,
					_request,
					"Send")),
			// TODO: Await the chat response from service?
		};

		CreateCanEditObservable();
	}

	public override WorkflowRequest GetResult() => _request;
}
