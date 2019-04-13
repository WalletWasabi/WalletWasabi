using AvalonStudio.Commands;
using ReactiveUI;
using System.Composition;
using System.IO;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class DiskCommands
	{
		[ImportingConstructor]
		public DiskCommands(CommandIconService commandIconService)
		{
			OpenDataFolderCommand = new CommandDefinition(
				"Data Folder",
				commandIconService.GetCompletionKindImage("FolderOpen"),
				ReactiveCommand.Create(OnOpenDataFolder));

			OpenWalletsFolderCommand = new CommandDefinition(
				"Wallets Folder",
				commandIconService.GetCompletionKindImage("FolderOpen"),
				ReactiveCommand.Create(OnOpenWalletsFolder));

			OpenLogFileCommand = new CommandDefinition(
				"Log File",
				commandIconService.GetCompletionKindImage("Log"),
				ReactiveCommand.Create(OnOpenLogFile));

			OpenTorLogFileCommand = new CommandDefinition(
				"Tor Log File",
				commandIconService.GetCompletionKindImage("Log"),
				ReactiveCommand.Create(OnOpenTorLogFile));

			OpenConfigFileCommand = new CommandDefinition(
				"Config File",
				commandIconService.GetCompletionKindImage("Settings"),
				ReactiveCommand.Create(OnOpenConfigFile));
		}

		private void OnOpenDataFolder()
		{
			IoHelpers.OpenFolderInFileExplorer(Global.GetDataDir());
		}

		private void OnOpenWalletsFolder()
		{
			IoHelpers.OpenFolderInFileExplorer(Global.WalletsDir);
		}

		private void OnOpenLogFile()
		{
			IoHelpers.OpenFileInTextEditor(Logging.Logger.FilePath);
		}

		private void OnOpenTorLogFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.GetTorLogsFile());
		}

		private void OnOpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}

		[ExportCommandDefinition("File.Open.DataFolder")]
		public CommandDefinition OpenDataFolderCommand { get; }

		[ExportCommandDefinition("File.Open.WalletsFolder")]
		public CommandDefinition OpenWalletsFolderCommand { get; }

		[ExportCommandDefinition("File.Open.LogFile")]
		public CommandDefinition OpenLogFileCommand { get; }

		[ExportCommandDefinition("File.Open.TorLogFile")]
		public CommandDefinition OpenTorLogFileCommand { get; }

		[ExportCommandDefinition("File.Open.ConfigFile")]
		public CommandDefinition OpenConfigFileCommand { get; }
	}
}
