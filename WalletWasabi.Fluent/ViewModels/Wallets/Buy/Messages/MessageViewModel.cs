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

	protected MessageViewModel(
		ChatMessage message,
		ICommand? editCommand,
		IObservable<bool>? canEditObservable)
	{
		_message = message;
		EditCommand = editCommand;
		CanEditObservable = canEditObservable;
	}

	public string? OriginalMessage { get; set; }

	public ICommand? EditCommand { get; }

	public IObservable<bool>? CanEditObservable { get; }
}
