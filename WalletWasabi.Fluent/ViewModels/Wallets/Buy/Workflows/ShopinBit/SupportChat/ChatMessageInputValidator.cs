using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ChatMessageInputValidator : TextInputInputValidator
{
	private readonly SupportChatWorkflowRequest _supportChatWorkflowRequest;

	public ChatMessageInputValidator(
		WorkflowState workflowState,
		SupportChatWorkflowRequest supportChatWorkflowRequest,
		string content)
		: base(workflowState, null, "Type here...", content)
	{
		_supportChatWorkflowRequest = supportChatWorkflowRequest;

		this.WhenAnyValue(x => x.Message)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		// TODO: Validate request.
		return !string.IsNullOrWhiteSpace(Message);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			var message = Message;

			_supportChatWorkflowRequest.Message = message;

			return message;
		}

		return null;
	}
}
