using System.Composition;
using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.IO;
using System.Linq;
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

			SettingsCommand = new CommandDefinition(
				"Settings",
				commandIconService.GetCompletionKindImage("Settings"),
				ReactiveCommand.Create(() => { }));
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

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }
	}
}
