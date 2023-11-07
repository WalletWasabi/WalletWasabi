using System.Windows.Input;

namespace WalletWasabi.Fluent.Extensions;

public static class CommandExtension
{
	public static void ExecuteIfCan(this ICommand command, object? commandParam = default)
	{
		if (command.CanExecute(commandParam))
		{
			command.Execute(commandParam);
		}
	}
}
