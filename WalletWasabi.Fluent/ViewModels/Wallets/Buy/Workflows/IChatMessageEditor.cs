using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IChatMessageEditor
{
	bool IsEditable(ChatMessage chatMessage);

	IWorkflowStep? GetEditor(ChatMessage chatMessage);
}
