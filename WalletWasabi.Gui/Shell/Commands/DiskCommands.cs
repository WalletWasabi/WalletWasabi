using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Composition;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class DiskCommands
	{
		[ImportingConstructor]
		public DiskCommands(CommandIconService commandIconService)
		{
			OpenDataFolderCommand = new CommandDefinition(
				"Data Folder",
				commandIconService.GetCompletionKindImage("DataFolder"),
				ReactiveCommand.Create(OnOpenDataFolder));
		}

		private void OnOpenDataFolder()
		{
			IoHelpers.OpenFolderInFileExplorer(Global.DataDir);
		}

		[ExportCommandDefinition("File.Open.DataFolder")]
		public CommandDefinition OpenDataFolderCommand { get; }
	}
}
