using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public partial class MessageViewModel : ReactiveObject
{
	[AutoNotify] private string? _message;
}
