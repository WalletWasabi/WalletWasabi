using System.Reactive;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public class CommandViewModel
{
	public CommandViewModel(string header, ReactiveCommand<Unit, Unit> command)
	{
		Header = header;
		Command = command;
	}

	public string Header { get; }
	public ReactiveCommand<Unit, Unit> Command { get; }
}
