using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels;

public class CommandViewModel
{
	public CommandViewModel(string name, ICommand command)
	{
		Name = name;
		Command = command;
	}

	public string Name { get; }
	public ICommand Command { get; }
}
