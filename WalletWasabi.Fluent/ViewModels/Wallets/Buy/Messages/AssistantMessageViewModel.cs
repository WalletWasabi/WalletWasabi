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
		string raw = message.Text;

		// Check if the string starts with '@'
		if (raw.StartsWith('@'))
		{
			// Find the index of the second '@'
			int secondAt = raw.IndexOf('@', 1);

			if (secondAt != -1)
			{
				// Take the substring starting after the second '@'
				raw = raw[(secondAt + 1)..];
			}
		}

		return raw;
	}
}
