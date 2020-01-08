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

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class DiskCommands
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
				commandIconService.GetCompletionKindImage("Settings"),
				onOpenConfigFile);
		}

		private void OnOpenDataFolder()
		{
			var global = Locator.Current.GetService<Global>();

			IoHelpers.OpenFolderInFileExplorer(global.DataDir);
		}

		private void OnOpenWalletsFolder()
		{
			var global = Locator.Current.GetService<Global>();

			IoHelpers.OpenFolderInFileExplorer(global.WalletsDir);
		}

		private async Task OnOpenLogFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath);
		}

		private async Task OnOpenTorLogFileAsync()
		{
			var global = Locator.Current.GetService<Global>();
			await FileHelpers.OpenFileInTextEditorAsync(global.TorLogsFile);
		}

		private async Task OnOpenConfigFileAsync()
		{
			var global = Locator.Current.GetService<Global>();
			await FileHelpers.OpenFileInTextEditorAsync(global.Config.FilePath);
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
