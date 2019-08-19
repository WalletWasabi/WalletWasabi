using Avalonia;
using Avalonia.Controls;
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

			var settingsCommand = ReactiveCommand.Create(() =>
			{
				IoC.Get<IShell>().AddOrSelectDocument(() => new SettingsViewModel(Global));
			});

			Observable
				.Merge(walletManagerCommand.ThrownExceptions)
				.Merge(settingsCommand.ThrownExceptions)
				.Subscribe(OnException);

			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager",
				commandIconService.GetCompletionKindImage("WalletManager"),
				walletManagerCommand);

			SettingsCommand = new CommandDefinition(
				"Settings",
				commandIconService.GetCompletionKindImage("Settings"),
				settingsCommand);

#if DEBUG
			DevToolsCommand = new CommandDefinition(
				"Dev Tools",
				commandIconService.GetCompletionKindImage("DevTools"),
				ReactiveCommand.Create(() =>
				{
					var devTools = new DevTools(Application.Current.Windows.FirstOrDefault());

					var devToolsWindow = new Window
					{
						Width = 1024,
						Height = 512,
						Content = devTools,
						DataTemplates =
						{
							new ViewLocator<Avalonia.Diagnostics.ViewModels.ViewModelBase>()
						}
					};

					devToolsWindow.Show();
				}));
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
			Logging.Logger.LogError<ToolCommands>(ex);
		}

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }

#if DEBUG

		[ExportCommandDefinition("Tools.DevTools")]
		public CommandDefinition DevToolsCommand { get; }

#endif
	}
}
