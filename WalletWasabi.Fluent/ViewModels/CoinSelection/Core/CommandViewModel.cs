using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Used in XAML via Style")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Used in XAML via Style")]
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
