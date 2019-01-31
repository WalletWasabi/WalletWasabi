using System.Composition;
using System.Threading.Tasks;
using AvalonStudio.Commands;
using ReactiveUI;
using System;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ExitCommands
	{
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public ExitCommands(CommandIconService commandIconService)
		{
			var exit = ReactiveCommand.CreateFromTask(OnExitAsync);

			exit.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning<ExitCommands>(ex));

			ExitCommand = new CommandDefinition(
			   "Exit",
			   commandIconService.GetCompletionKindImage("Exit"),
			   exit);
		}

		private async Task OnExitAsync()
		{
			await Global.QuitApplicationAsync();
		}
	}
}
