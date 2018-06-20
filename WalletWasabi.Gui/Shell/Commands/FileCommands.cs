using AvalonStudio.Commands;
using ReactiveUI;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ExitCommands
	{
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		public ExitCommands()
		{
			ExitCommand = new CommandDefinition(
				"Exit", null, ReactiveCommand.Create(OnExit));
		}

		private void OnExit()
		{
		}
	}
}
