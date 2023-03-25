namespace WalletWasabi.Fluent.ViewModels.ChatGPT.Messages.Actions;

public partial class BalanceActionMessageViewModel : MessageViewModel
{
	[AutoNotify] private string? _balance;
}
