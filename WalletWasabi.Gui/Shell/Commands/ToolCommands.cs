using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Composition;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Gui.Tabs.WalletManager;

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
			var encCommand = ReactiveCommand.Create(OnEncryptionManager).DisposeWith(Disposables);

			EncryptionManagerCommand = new CommandDefinition(
						commandIconService.GetCompletionKindImage("EncryptionManager"),
						encCommand);

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
			encryptionManagerViewModel.SelectTab(EncryptionManagerViewModel.Tabs.Encrypt);
		}

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.EncryptionManager")]
		public CommandDefinition EncryptionManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }
	}
}
