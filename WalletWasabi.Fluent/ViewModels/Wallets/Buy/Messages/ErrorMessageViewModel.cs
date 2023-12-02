using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class ErrorMessageViewModel : MessageViewModel
{
	public ErrorMessageViewModel(
		ICommand? editCommand,
		IObservable<bool>? canEditObservable) : base(editCommand, canEditObservable)
	{
	}
}
