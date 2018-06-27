using System.Composition;
using Avalonia;
using AvalonStudio.Commands;
using ReactiveUI;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ExitCommands
	{
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public ExitCommands(CommandIconService commandIconService)
		{
			ExitCommand = new CommandDefinition(
			   "Exit",
			   commandIconService.GetCompletionKindImage("Exit"),
			   ReactiveCommand.Create(OnExit));
		}

		private void OnExit()
		{
			Application.Current.Exit();
		}
	}
}
