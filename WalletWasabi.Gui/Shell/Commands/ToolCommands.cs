using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics;
using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ToolCommands
	{
		[ImportingConstructor]
		public ToolCommands(CommandIconService commandIconService)
		{
			var walletManagerCommand = ReactiveCommand.Create(OnWalletManager);

			var settingsCommand = ReactiveCommand.Create(() =>
				IoC.Get<IShell>().AddOrSelectDocument(() => new SettingsViewModel()));

			var transactionBroadcasterCommand = ReactiveCommand.Create(() =>
				IoC.Get<IShell>().AddOrSelectDocument(() => new TransactionBroadcasterViewModel()));

#if DEBUG
			var devToolsCommand = ReactiveCommand.Create(() =>
				DevToolsExtensions.OpenDevTools((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow));
#endif
			Observable
				.Merge(walletManagerCommand.ThrownExceptions)
				.Merge(settingsCommand.ThrownExceptions)
				.Merge(transactionBroadcasterCommand.ThrownExceptions)
#if DEBUG
				.Merge(devToolsCommand.ThrownExceptions)
#endif
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));

			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager",
				commandIconService.GetCompletionKindImage("WalletManager"),
				walletManagerCommand);

			SettingsCommand = new CommandDefinition(
				"Settings",
				commandIconService.GetCompletionKindImage("Settings"),
				settingsCommand);

			TransactionBroadcasterCommand = new CommandDefinition(
				"Transaction Broadcaster",
				commandIconService.GetCompletionKindImage("BroadcastTransaction"),
				transactionBroadcasterCommand);

#if DEBUG
			DevToolsCommand = new CommandDefinition(
				"Dev Tools",
				commandIconService.GetCompletionKindImage("DevTools"),
				devToolsCommand);
#endif
		}

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }

		[ExportCommandDefinition("Tools.BroadcastTransaction")]
		public CommandDefinition TransactionBroadcasterCommand { get; }

#if DEBUG

		[ExportCommandDefinition("Tools.DevTools")]
		public CommandDefinition DevToolsCommand { get; }

#endif

		private void OnWalletManager()
		{
			var global = Locator.Current.GetService<Global>();

			var walletManagerViewModel = IoC.Get<IShell>().GetOrCreateByType<WalletManagerViewModel>();
			if (global.WalletManager.WalletDirectories.EnumerateWalletFiles().Any())
			{
				walletManagerViewModel.SelectLoadWallet();
			}
			else
			{
				walletManagerViewModel.SelectGenerateWallet();
			}
		}
	}
}
