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
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ToolCommands : IDisposable
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		[ImportingConstructor]
		public ToolCommands(CommandIconService commandIconService)
		{
			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager",
				commandIconService.GetCompletionKindImage("WalletManager"),
				ReactiveCommand.Create(OnWalletManager));                    //syncronize with the UI

			var encCommand = ReactiveCommand.Create(OnEncryptionManager).DisposeWith(Disposables);

			EncryptionManagerCommand = new CommandDefinition(
						"Encryption Manager",
						commandIconService.GetCompletionKindImage("EncryptionManager"),
						encCommand);

			SettingsCommand = new CommandDefinition(
				"Settings",
				commandIconService.GetCompletionKindImage("Settings"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new SettingsViewModel());
				}).DisposeWith(Disposables));
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

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables.Dispose();
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
