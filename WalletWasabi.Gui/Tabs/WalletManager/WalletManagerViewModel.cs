using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class WalletManagerViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;

		public WalletManagerViewModel() : base("Wallet Manager")
		{
			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this),
				new RecoverWalletViewModel(this),
				new LoadWalletViewModel(this, LoadWalletType.Desktop),
				new LoadWalletViewModel(this, LoadWalletType.Password),
				new LoadWalletViewModel(this, LoadWalletType.Hardware)
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
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && (((LoadWalletViewModel)x).LoadWalletType == LoadWalletType.Desktop));
		}

		public void SelectTestPassword()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && (((LoadWalletViewModel)x).LoadWalletType == LoadWalletType.Password));
		}

		public void SelectHardwareWallet()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && (((LoadWalletViewModel)x).LoadWalletType == LoadWalletType.Hardware));
		}

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}
	}
}
