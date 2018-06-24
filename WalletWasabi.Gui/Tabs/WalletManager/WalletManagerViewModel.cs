using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using System;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class WalletManagerViewModel : DocumentTabViewModel
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
				new LoadWalletViewModel(this)
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
			get { return _categories; }
			set { this.RaiseAndSetIfChanged(ref _categories, value); }
		}

		public CategoryViewModel SelectedCategory
		{
			get { return _selectedCategory; }
			set { this.RaiseAndSetIfChanged(ref _selectedCategory, value); }
		}

		public void SelectGenerateWallet()
		{
			SelectedCategory = Categories.First(x=>x is GenerateWalletViewModel);
		}

		public void SelectRecoverWallet()
		{
			SelectedCategory = Categories.First(x=>x is RecoverWalletViewModel);
		}

		public void SelectLoadWallet()
		{
			SelectedCategory = Categories.First(x=>x is LoadWalletViewModel);
		}

		public ViewModelBase CurrentView
		{
			get { return _currentView; }
			set { this.RaiseAndSetIfChanged(ref _currentView, value); }
		}

		internal void RemoveLoadWalletOption()
		{
			SelectGenerateWallet();
			Categories.Remove(Categories.First(x=>x is LoadWalletViewModel));
		}
	}
}
