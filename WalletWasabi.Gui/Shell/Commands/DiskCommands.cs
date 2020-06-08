using AvalonStudio.Commands;
using ReactiveUI;
using Splat;
using System;
using System.Composition;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Shell.Commands
{
	public class DiskCommands
	{
		[ImportingConstructor]
		public DiskCommands(CommandIconService commandIconService)
		{
			var onOpenDataFolder = ReactiveCommand.Create(OnOpenDataFolder);
			var onOpenWalletsFolder = ReactiveCommand.Create(OnOpenWalletsFolder);
			var onOpenLogFile = ReactiveCommand.CreateFromTask(OnOpenLogFileAsync);
			var onOpenTorLogFile = ReactiveCommand.CreateFromTask(OnOpenTorLogFileAsync);
			var onOpenConfigFile = ReactiveCommand.CreateFromTask(OnOpenConfigFileAsync);

			Observable
				.Merge(onOpenConfigFile.ThrownExceptions)
				.Merge(onOpenWalletsFolder.ThrownExceptions)
				.Merge(onOpenLogFile.ThrownExceptions)
				.Merge(onOpenTorLogFile.ThrownExceptions)
				.Merge(onOpenConfigFile.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));

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
				commandIconService.GetCompletionKindImage("SettingsFile"),
				onOpenConfigFile);
		}

		private static Config Config { get; set; }
		private static string TorLogsFile { get; set; }
		
		private static string DataDir { get; set; }

		private static WalletManager WalletManager { get; set; }

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

		public static void InjectDependencies(Config config, string dataDir, string torLogsFile)
		{
			Config = config;
			DataDir = dataDir;
			TorLogsFile = torLogsFile;
		}

		private void OnOpenDataFolder()
		{
			IoHelpers.OpenFolderInFileExplorer(DataDir);
		}

		private void OnOpenWalletsFolder()
		{
			IoHelpers.OpenFolderInFileExplorer(WalletManager.WalletDirectories.WalletsDir);
		}

		private async Task OnOpenLogFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath);
		}

		private async Task OnOpenTorLogFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(TorLogsFile);
		}

		private async Task OnOpenConfigFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(Config.FilePath);
		}
	}
}
