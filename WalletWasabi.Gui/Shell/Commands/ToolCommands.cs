using System.Composition;
using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Gui.Tabs.EncryptionManager;
using System.Reactive.Linq;
using System.ComponentModel;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ToolCommands
	{
		[ImportingConstructor]
		public ToolCommands(CommandIconService commandIconService)
		{
			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager",
				commandIconService.GetCompletionKindImage("WalletManager"),
				ReactiveCommand.Create(OnWalletManager));

			var isWalletLoaded = Global.WhenPropertyChanged
				.Where(x => x.PropertyName == nameof(WalletService))  //looking for the wallet is loaded
				.Select(ws => ws != null)                                       //if it is not null -> wallet is loaded
				.ObserveOn(RxApp.MainThreadScheduler);                          //syncronize with the UI

			EncryptionManagerCommand = new CommandDefinition(
				"Encryption Manager",
				commandIconService.GetCompletionKindImage("EncryptionManager"),
				ReactiveCommand.Create(OnEncryptionManager, isWalletLoaded));

			SettingsCommand = new CommandDefinition(
				"Settings",
				commandIconService.GetCompletionKindImage("Settings"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new SettingsViewModel());
				}));
		}

		private void OnWalletManager()
		{
			var isAnyWalletAvailable = Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any();

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

		private void OnEncryptionManager()
		{
			var encryptionManagerViewModel = IoC.Get<IShell>().GetOrCreate<EncryptionManagerViewModel>();
			encryptionManagerViewModel.SelectSignMessage();
		}

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.EncryptionManager")]
		public CommandDefinition EncryptionManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }
	}
}
