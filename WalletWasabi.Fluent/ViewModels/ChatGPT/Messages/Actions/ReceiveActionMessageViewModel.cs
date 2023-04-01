namespace WalletWasabi.Fluent.ViewModels.ChatGPT.Messages.Actions;

public partial class ReceiveActionMessageViewModel : MessageViewModel
{
	[AutoNotify] private string? _address;
}
