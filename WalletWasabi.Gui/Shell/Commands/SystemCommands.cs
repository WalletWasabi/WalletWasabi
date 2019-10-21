using Avalonia;
using AvalonStudio.Commands;
using ReactiveUI;
using System;
using System.Composition;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class SystemCommands
	{
		public Global Global { get; }

		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public SystemCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = Guard.NotNull(nameof(Global), global.Global);

			var exit = ReactiveCommand.Create(OnExit);

			exit.ThrownExceptions.Subscribe(ex => Logger.LogWarning(ex));

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
