using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public abstract partial class TextInputInputValidator : InputValidator
{
	protected TextInputInputValidator(
		WorkflowState workflowState,
		Func<string?>? messageProvider,
		string? watermark,
		string? content = "Next",
		ChatMessageMetaData.ChatMessageTag tag = ChatMessageMetaData.ChatMessageTag.None) : base(workflowState, messageProvider, watermark, content, tag)
	{
	}
}
