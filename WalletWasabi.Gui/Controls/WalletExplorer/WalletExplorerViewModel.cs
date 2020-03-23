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

		public override Location DefaultLocation => Location.Right;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModelBase>();

			WalletManager = Locator.Current.GetService<Global>().WalletManager;
		}

		private WalletManager WalletManager { get; }

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

		internal void OpenWallet(Wallet wallet, bool receiveDominant, bool select = true)
		{
			if (_wallets.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				return;
			}

			WalletViewModel walletViewModel = new WalletViewModel(wallet);

			Wallets.InsertSorted(walletViewModel);

			if (select)
			{
				SelectedItem = walletViewModel;
			}

			walletViewModel.OpenWallet(receiveDominant);
		}

		internal void RemoveWallet(WalletViewModelBase wallet)
		{
			Wallets.Remove(wallet);
		}

		private void LoadWallets()
		{
			foreach (var wallet in WalletManager
				.GetKeyManagers()
				.Where(x => !x.IsWatchOnly)) // If password isn't required then add the wallet, otherwise add only not watchonly wallets.				
				
			{
				Wallets.InsertSorted(new ClosedWalletViewModel(WalletManager.GetWalletByName(wallet.WalletName)));
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
