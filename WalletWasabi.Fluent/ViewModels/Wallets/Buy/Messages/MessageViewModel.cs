using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public abstract partial class MessageViewModel : ReactiveObject
{
	private ChatMessage _message;

	[AutoNotify] private string? _id;
	[AutoNotify] private string? _uiMessage;
	[AutoNotify] private bool _isUnread;
	[AutoNotify] private bool _isPaid; // TODO: Should only be in PayNowAssistantMessageViewModel

	protected MessageViewModel(ChatMessage message)
	{
		_message = message;
		IsUnread = message.IsUnread;
		OriginalText = message.Text;
	}

	public string? OriginalText { get; set; }
}
