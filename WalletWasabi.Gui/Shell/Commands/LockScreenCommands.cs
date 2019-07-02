using Avalonia;
using AvalonStudio.Commands;
using ReactiveUI;
using System;
using System.Composition;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class LockScreenCommands
	{
		[ExportCommandDefinition("File.LockScreen")]
		public CommandDefinition LockScreenCommand { get; }

		[ImportingConstructor]
		public LockScreenCommands(CommandIconService commandIconService)
		{
			var lockScreen = ReactiveCommand.Create(OnLockScreen);

			lockScreen.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning<LockScreenCommands>(ex));

			LockScreenCommand = new CommandDefinition(
			   "Lock Screen",
			   commandIconService.GetCompletionKindImage("Lock"),
			   lockScreen);
		}

		private void OnLockScreen()
		{

		}
	}
}
