using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class DiskCommands : IDisposable
	{
		private CompositeDisposable Disposables { get; }

		[ImportingConstructor]
		public DiskCommands(CommandIconService commandIconService)
		{
			Disposables = new CompositeDisposable();

			OpenDataFolderCommand = new CommandDefinition(
				"Data Folder",
				commandIconService.GetCompletionKindImage("FolderOpen"),
				ReactiveCommand.Create(OnOpenDataFolder).DisposeWith(Disposables));

			OpenWalletsFolderCommand = new CommandDefinition(
				"Wallets Folder",
				commandIconService.GetCompletionKindImage("FolderOpen"),
				ReactiveCommand.Create(OnOpenWalletsFolder).DisposeWith(Disposables));

			OpenLogFileCommand = new CommandDefinition(
				"Log File",
				commandIconService.GetCompletionKindImage("Log"),
				ReactiveCommand.Create(OnOpenLogFile).DisposeWith(Disposables));

			OpenTorLogFileCommand = new CommandDefinition(
				"Tor Log File",
				commandIconService.GetCompletionKindImage("Log"),
				ReactiveCommand.Create(OnOpenTorLogFile).DisposeWith(Disposables));

			OpenConfigFileCommand = new CommandDefinition(
				"Config File",
				commandIconService.GetCompletionKindImage("Settings"),
				ReactiveCommand.Create(OnOpenConfigFile).DisposeWith(Disposables));
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

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
