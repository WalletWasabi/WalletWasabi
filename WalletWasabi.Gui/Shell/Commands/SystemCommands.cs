using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

		[DefaultKeyGesture("ALT+F4")]
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[DefaultKeyGesture("CTRL+L", osxKeyGesture: "CMD+L")]
		[ExportCommandDefinition("File.LockScreen")]
		public CommandDefinition LockScreenCommand {get;}

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

				#pragma warning disable IDE0053 // Use expression body for lambda expressions
				var lockCommand = ReactiveCommand.Create(() => { Global.UiConfig.LockScreenActive = true; });
				#pragma warning restore IDE0053 // Use expression body for lambda expressions

			lockCommand.ThrownExceptions.Subscribe(ex => Logger.LogWarning(ex));

			LockScreenCommand = new CommandDefinition(
				"Lock Screen",
				commandIconService.GetCompletionKindImage("Exit"),
				lockCommand);
		}

		private void OnExit()
		{
			(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.Close();
		}
	}
}
