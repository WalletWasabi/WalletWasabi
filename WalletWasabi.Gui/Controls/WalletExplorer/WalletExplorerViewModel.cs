using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Text;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using System.Linq;
using AvalonStudio.Shell;
using WalletWasabi.Services;
using System.IO;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[Export]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IActivatableExtension, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		public override Location DefaultLocation => Location.Right;

		public WalletExplorerViewModel()
		{
			Disposables = new CompositeDisposable();

			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModel>();
		}

		private ObservableCollection<WalletViewModel> _wallets;

		public ObservableCollection<WalletViewModel> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		private WasabiDocumentTabViewModel _selectedItem;

		public WasabiDocumentTabViewModel SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		internal void OpenWallet(WalletService walletService, bool receiveDominant)
		{
			var walletName = Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath);
			if (_wallets.Any(x => x.Title == walletName))
				return;

			WalletViewModel walletViewModel = new WalletViewModel(walletService, receiveDominant).DisposeWith(Disposables);
			_wallets.Add(walletViewModel);
		}

		public void BeforeActivation()
		{
		}

		public void Activation()
		{
			IoC.Get<IShell>().MainPerspective.AddOrSelectTool(this);
		}

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

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
