using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class AssistantMessageViewModel : MessageViewModel
{
	public AssistantMessageViewModel(
		ICommand? editCommand,
		IObservable<bool>? canEditObservable) : base(editCommand, canEditObservable)
	{
	}
}
