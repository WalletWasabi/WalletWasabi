using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class FirstNameInputValidator : TextInputInputValidator
{
	public FirstNameInputValidator(
		WorkflowState workflowState,
		ChatMessageMetaData.ChatMessageTag tag)
		: base(workflowState, null, "Type here...", tag: tag)
	{
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
			return Message;
		}

		return null;
	}
}
