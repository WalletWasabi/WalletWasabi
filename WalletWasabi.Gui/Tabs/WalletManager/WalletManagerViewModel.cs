using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Disposables;
using WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets;
using WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets;
using WalletWasabi.Gui.Tabs.WalletManager.LoadWallets;
using WalletWasabi.Gui.Tabs.WalletManager.RecoverWallets;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	[Export]
	[Shared]
	public class WalletManagerViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;
		private LoadWalletViewModel LoadWalletDesktop { get; set; }

		public WalletManagerViewModel() : base("Wallet Manager")
		{
		}

		public ObservableCollection<CategoryViewModel> Categories
		{
			get => _categories;
			set => this.RaiseAndSetIfChanged(ref _categories, value);
		}

		public CategoryViewModel SelectedCategory
		{
			get => _selectedCategory;
			set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
		}

		public void SelectGenerateWallet()
		{
			SelectedCategory = Categories.First(x => x is GenerateWalletViewModel);
		}

		public void SelectRecoverWallet()
		{
			SelectedCategory = Categories.First(x => x is RecoverWalletViewModel);
		}

		public void SelectLoadWallet()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel model && model.LoadWalletType == LoadWalletType.Desktop);
		}

		public void SelectTestPassword(string walletName)
		{
			var passwordTestViewModel = Categories.OfType<LoadWalletViewModel>().First(x => x.LoadWalletType == LoadWalletType.Password);
			SelectedCategory = passwordTestViewModel;

			passwordTestViewModel.SelectedWallet = passwordTestViewModel.Wallets.FirstOrDefault(w => w.WalletName == walletName);
		}

		public override bool OnClose()
		{
			foreach (var tab in Categories.OfType<IDisposable>())
			{
				tab.Dispose();
			}

			return base.OnClose();
		}

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}
	}
}
