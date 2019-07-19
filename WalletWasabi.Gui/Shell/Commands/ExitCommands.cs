using Avalonia;
using AvalonStudio.Commands;
using ReactiveUI;
using System;
using System.Composition;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ExitCommands
	{
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public ExitCommands(CommandIconService commandIconService)
		{
			var exit = ReactiveCommand.Create(OnExit);

			exit.ThrownExceptions.Subscribe(Logging.Logger.LogWarning<ExitCommands>);

			ExitCommand = new CommandDefinition(
				"Exit",
				commandIconService.GetCompletionKindImage("Exit"),
				exit);
		}

		private void OnExit()
		{
			Application.Current.MainWindow.Close();
		}
	}
}
