using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics;
using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
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
		public Global Global { get; }

		[ImportingConstructor]
		public ToolCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = global.Global;
			var walletManagerCommand = ReactiveCommand.Create(OnWalletManager);

			var settingsCommand = ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new SettingsViewModel(Global)));

			var transactionBroadcasterCommand = ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new TransactionBroadcasterViewModel(Global)));

#if DEBUG
			var devToolsCommand = ReactiveCommand.Create(() =>
			{
				DevToolsExtensions.OpenDevTools((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
			});
#endif

			Observable
				.Merge(walletManagerCommand.ThrownExceptions)
				.Merge(settingsCommand.ThrownExceptions)
#if DEBUG
				.Merge(devToolsCommand.ThrownExceptions)
#endif
				.Subscribe(OnException);

			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager",
				commandIconService.GetCompletionKindImage("WalletManager"),
				walletManagerCommand);

			SettingsCommand = new CommandDefinition(
				"Settings",
				commandIconService.GetCompletionKindImage("Settings"),
				settingsCommand);

			TransactionBroadcasterCommand = new CommandDefinition(
				"Broadcast Transaction",
				commandIconService.GetCompletionKindImage("BroadcastTransaction"),
				transationBroadcasterCommand);

#if DEBUG
			DevToolsCommand = new CommandDefinition(
				"Dev Tools",
				commandIconService.GetCompletionKindImage("DevTools"),
				devToolsCommand);
#endif
		}

		private void OnWalletManager()
		{
			var walletManagerViewModel = IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>();
			if (Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any())
			{
				walletManagerViewModel.SelectLoadWallet();
			}
			else
			{
				walletManagerViewModel.SelectGenerateWallet();
			}
		}

		private void OnException(Exception ex)
		{
			Logger.LogError(ex);
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
	}
}
