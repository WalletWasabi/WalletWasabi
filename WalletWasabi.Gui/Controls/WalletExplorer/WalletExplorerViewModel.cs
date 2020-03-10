using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[Export]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IActivatableExtension
	{
		private ObservableCollection<WalletViewModelBase> _wallets;
		private ViewModelBase _selectedItem;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModelBase>();

			WalletManager = Locator.Current.GetService<Global>().WalletManager;
		}

		private WalletManager WalletManager { get; }

		public override Location DefaultLocation => Location.Right;

		public ObservableCollection<WalletViewModelBase> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		public ViewModelBase SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		internal void RemoveWallet(WalletViewModelBase wallet)
		{
			Wallets.Remove(wallet);
		}

		internal void OpenWallet(WalletService walletService, bool receiveDominant)
		{
			var walletName = Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath);
			if (_wallets.Any(x => x.Title == walletName))
			{
				return;
			}

			WalletViewModel walletViewModel = new WalletViewModel(walletService, receiveDominant);
			Wallets.InsertSorted(walletViewModel);
			walletViewModel.OnWalletOpened();

			// TODO if we ever implement closing a wallet OnWalletClosed needs to be called
			// to prevent memory leaks.
		}

		private void LoadWallets ()
		{
			foreach (var walletPath in WalletManager.EnumerateWalletFiles())
			{
				Wallets.InsertSorted(new ClosedWalletViewModel(walletPath));
			}
		}

		public void BeforeActivation()
		{
		}

		public void Activation()
		{
			IoC.Get<IShell>().MainPerspective.AddOrSelectTool(this);

			LoadWallets();
		}
	}
}
