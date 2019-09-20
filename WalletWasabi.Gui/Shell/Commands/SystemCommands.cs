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

		[DefaultKeyGesture("ALT+F4", osxKeyGesture: "CMD+Q")]
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public SystemCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = Guard.NotNull(nameof(Global), global.Global);

			var exit = ReactiveCommand.Create(OnExit);

			exit.ThrownExceptions.Subscribe(ex => Logger.LogWarning(ex));

			ExitCommand = new CommandDefinition(
				"Quit Wasabi Wallet",
				commandIconService.GetCompletionKindImage("Exit"),
				exit);
		}

		private void OnExit()
		{
			(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.Close();
		}
	}
}
