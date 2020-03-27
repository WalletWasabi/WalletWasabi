using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets;
using WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets;
using WalletWasabi.Gui.Tabs.WalletManager.LoadWallets;
using WalletWasabi.Gui.Tabs.WalletManager.RecoverWallets;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	[Export]
	[Shared]
	public class WalletManagerViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;
		private LoadWalletViewModel LoadWalletViewModelDesktop { get; }

		public WalletManagerViewModel() : base("Wallet Manager")
		{
			LoadWalletViewModelDesktop = new LoadWalletViewModel(this, LoadWalletType.Desktop);

			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this),
				new RecoverWalletViewModel(this),
				LoadWalletViewModelDesktop,
				new LoadWalletViewModel(this, LoadWalletType.Password),
				new ConnectHardwareWalletViewModel(this)
			};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
				{
					category?.OnCategorySelected();

					CurrentView = category;
				});
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

		public void SelectTestPassword()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel model && model.LoadWalletType == LoadWalletType.Password);
		}

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}

		public override bool OnClose()
		{
			foreach (var tab in Categories.OfType<IDisposable>())
			{
				tab.Dispose();
			}
			return base.OnClose();
		}
	}
}
