using Avalonia;
using AvalonStudio.Commands;
using ReactiveUI;
using System;
using System.Composition;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class SystemCommands
	{
		public Global Global { get; }

		[ExportCommandDefinition("File.LockScreen")]
		public CommandDefinition LockScreenCommand { get; }

		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public SystemCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = Guard.NotNull(nameof(Global), global.Global);

			var lockScreen = ReactiveCommand.Create(OnLockScreen);
			var exit = ReactiveCommand.Create(OnExit);

			lockScreen.ThrownExceptions.Subscribe(Logging.Logger.LogWarning<SystemCommands>);
			exit.ThrownExceptions.Subscribe(Logging.Logger.LogWarning<SystemCommands>);

			LockScreenCommand = new CommandDefinition(
			   "Lock Screen",
			   commandIconService.GetCompletionKindImage("Lock"),
			   lockScreen);

			ExitCommand = new CommandDefinition(
			   "Exit",
			   commandIconService.GetCompletionKindImage("Exit"),
			   exit);
		}

		private void OnLockScreen()
		{
			Global.UiConfig.LockScreenActive = true;
		}

		private void OnExit()
		{
			Application.Current.MainWindow.Close();
		}
	}
}
