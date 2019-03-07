using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Composition;
using System.Linq;
using System.Reactive.Disposables;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class WalletCommands : IDisposable
	{
		private CompositeDisposable Disposables { get; }

		[ImportingConstructor]
		public WalletCommands(CommandIconService commandIconService)
		{
			Disposables = new CompositeDisposable();

			GenerateWalletCommand = new CommandDefinition(
				"Generate Wallet",
				commandIconService.GetCompletionKindImage("GenerateWallet"),
				ReactiveCommand.Create(OnGenerateWallet).DisposeWith(Disposables));

			RecoverWalletCommand = new CommandDefinition(
				"Recover Wallet",
				commandIconService.GetCompletionKindImage("RecoverWallet"),
				ReactiveCommand.Create(OnRecoverWallet).DisposeWith(Disposables));

			LoadWallet = new CommandDefinition(
				"Load Wallet",
				commandIconService.GetCompletionKindImage("LoadWallet"),
				ReactiveCommand.Create(OnLoadWallet).DisposeWith(Disposables));
		}

		private void OnGenerateWallet()
		{
			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectGenerateWallet();
		}

		private void OnRecoverWallet()
		{
			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectRecoverWallet();
		}

		private void OnLoadWallet()
		{
			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectLoadWallet();
		}

		[ExportCommandDefinition("File.GenerateWallet")]
		public CommandDefinition GenerateWalletCommand { get; }

		[ExportCommandDefinition("File.RecoverWallet")]
		public CommandDefinition RecoverWalletCommand { get; }

		[ExportCommandDefinition("File.LoadWallet")]
		public CommandDefinition LoadWallet { get; }

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
