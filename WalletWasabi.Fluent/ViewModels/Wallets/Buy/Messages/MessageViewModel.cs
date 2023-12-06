using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public abstract partial class MessageViewModel : ReactiveObject
{
	[AutoNotify] private string? _id;
	[AutoNotify] private string? _message;
	[AutoNotify] private bool _isUnread;

	protected MessageViewModel(
		ICommand? editCommand,
		IObservable<bool>? canEditObservable)
	{
		EditCommand = editCommand;
		CanEditObservable = canEditObservable;
	}

	public ICommand? EditCommand { get; }

	public IObservable<bool>? CanEditObservable { get; }
}
