using AvalonStudio.Commands;
using ReactiveUI;
using System;
using System.Composition;
using System.IO;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class DiskCommands
	{
		private readonly Global Global;

		[ImportingConstructor]
		public DiskCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = global.Global;
			var onOpenDataFolder = ReactiveCommand.Create(OnOpenDataFolder);
			var onOpenWalletsFolder = ReactiveCommand.Create(OnOpenWalletsFolder);
			var onOpenLogFile = ReactiveCommand.Create(OnOpenLogFile);
			var onOpenTorLogFile = ReactiveCommand.Create(OnOpenTorLogFile);
			var onOpenConfigFile = ReactiveCommand.Create(OnOpenConfigFile);

			Observable
				.Merge(onOpenConfigFile.ThrownExceptions)
				.Merge(onOpenWalletsFolder.ThrownExceptions)
				.Merge(onOpenLogFile.ThrownExceptions)
				.Merge(onOpenTorLogFile.ThrownExceptions)
				.Merge(onOpenConfigFile.ThrownExceptions)
				.Subscribe(OnFileOpenException);

			OpenDataFolderCommand = new CommandDefinition(
				"Data Folder",
				commandIconService.GetCompletionKindImage("FolderOpen"),
				onOpenDataFolder);

			OpenWalletsFolderCommand = new CommandDefinition(
				"Wallets Folder",
				commandIconService.GetCompletionKindImage("FolderOpen"),
				onOpenWalletsFolder);

			OpenLogFileCommand = new CommandDefinition(
				"Log File",
				commandIconService.GetCompletionKindImage("Log"),
				onOpenLogFile);

			OpenTorLogFileCommand = new CommandDefinition(
				"Tor Log File",
				commandIconService.GetCompletionKindImage("Log"),
				onOpenTorLogFile);

			OpenConfigFileCommand = new CommandDefinition(
				"Config File",
				commandIconService.GetCompletionKindImage("Settings"),
				onOpenConfigFile);
		}

		private void OnOpenDataFolder()
		{
			IoHelpers.OpenFolderInFileExplorer(Global.DataDir);
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
			IoHelpers.OpenFileInTextEditor(Global.TorLogsFile);
		}

		private void OnOpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}

		private void OnFileOpenException(Exception ex)
		{
			Logging.Logger.LogError(ex);
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
