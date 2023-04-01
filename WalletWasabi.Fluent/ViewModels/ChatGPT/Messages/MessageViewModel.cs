using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT.Messages;

public partial class MessageViewModel : ReactiveObject
{
	[AutoNotify] private string? _message;
}
