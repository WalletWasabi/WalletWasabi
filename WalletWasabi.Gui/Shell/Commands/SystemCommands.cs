using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvalonStudio.Commands;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Composition;
using System.Reactive.Linq;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Shell.Commands
{
	public class SystemCommands
	{
		[ImportingConstructor]
		public SystemCommands(CommandIconService commandIconService)
		{
			ExitCommand = new CommandDefinition(
				"Exit",
				commandIconService.GetCompletionKindImage("Exit"),
				ReactiveCommand.Create(OnExit));

			LockScreenCommand = new CommandDefinition(
				"Lock Screen",
				commandIconService.GetCompletionKindImage("Lock"),
				ReactiveCommand.Create(() => UiConfig.LockScreenActive = true));

			Observable
				.Merge(ExitCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(ExitCommand.GetReactiveCommand().ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogWarning(ex));
		}

		public static UiConfig UiConfig { get; private set; }

		[DefaultKeyGesture("ALT+F4")]
		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[DefaultKeyGesture("CTRL+L", osxKeyGesture: "CMD+L")]
		[ExportCommandDefinition("File.LockScreen")]
		public CommandDefinition LockScreenCommand { get; }

		public static void InjectDependencies(UiConfig uiConfig)
		{
			UiConfig = uiConfig;
		}

		private void OnExit()
		{
			(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.Close();
		}
	}
}
