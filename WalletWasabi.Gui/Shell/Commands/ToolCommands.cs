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
		public ToolCommands()
		{
			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager", null, ReactiveCommand.Create(OnWalletManager));

			SettingsCommand = new CommandDefinition("Settings", null, ReactiveCommand.Create(() => { }));
		}

		private void OnWalletManager()
		{
			if (Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any())
			{
				// Load
				IShell tabs = IoC.Get<IShell>();
				IDocumentTabViewModel doc = tabs.Documents.FirstOrDefault(x => x is WalletManagerViewModel);
				if (doc != default)
				{
					var document = doc as WalletManagerViewModel;
					tabs.SelectedDocument = doc;
					document.SelectedCategory = document.Categories.First(x => x is LoadWalletViewModel);
				}
				else
				{
					var document = new WalletManagerViewModel();
					tabs.AddDocument(document);
					document.SelectedCategory = document.Categories.First(x => x is LoadWalletViewModel);
				}
			}
			else
			{
				// Generate
				IShell tabs = IoC.Get<IShell>();
				IDocumentTabViewModel doc = tabs.Documents.FirstOrDefault(x => x is WalletManagerViewModel);
				if (doc != default)
				{
					var document = doc as WalletManagerViewModel;
					tabs.SelectedDocument = doc;
					document.SelectedCategory = document.Categories.First(x => x is GenerateWalletViewModel);
				}
				else
				{
					var document = new WalletManagerViewModel();
					tabs.AddDocument(document);
					document.SelectedCategory = document.Categories.First(x => x is GenerateWalletViewModel);
				}
			}
		}

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }
	}
}
