using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IMessageEditor
{
	bool IsEditable(ChatMessage chatMessage);

	IWorkflowStep? Get(ChatMessage chatMessage);
}
