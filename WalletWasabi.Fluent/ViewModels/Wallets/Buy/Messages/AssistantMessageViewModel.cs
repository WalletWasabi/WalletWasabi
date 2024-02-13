using System.Linq;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class AssistantMessageViewModel : MessageViewModel
{
	public AssistantMessageViewModel(ChatMessage message) : base(message)
	{
		string uiMessage = ParseRawMessage(message);
		UiMessage = uiMessage;
	}

	private string ParseRawMessage(ChatMessage message)
	{
		// Remove @timestamp@ from message.
		string raw = message.Text;
		if (raw.Contains('@'))
		{
			raw = raw.Split('@').Last();
		}
		return raw;
	}
}
