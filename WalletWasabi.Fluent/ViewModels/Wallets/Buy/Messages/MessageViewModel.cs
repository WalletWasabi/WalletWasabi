using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public abstract partial class MessageViewModel : ReactiveObject
{
	[AutoNotify] private string? _message;
	[AutoNotify] private bool? _isUnread;
}
