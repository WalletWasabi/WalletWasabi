using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public abstract partial class MessageViewModel : ReactiveObject
{
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private ChatMessage _message;
	[AutoNotify] private string? _id;
	[AutoNotify] private string? _uiMessage;
	[AutoNotify] private bool _isUnread;

	protected MessageViewModel(ChatMessage message)
	{
		_message = message;
		IsUnread = message.IsUnread;
		OriginalText = message.Text;
	}

	public string? OriginalText { get; set; }
}
