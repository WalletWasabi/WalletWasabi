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
			LoadWalletCategory = new LoadWalletViewModel();

			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this),
				new RecoverWalletViewModel(this),
				LoadWalletCategory
			};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
			{
				category?.OnCategorySelected();

				CurrentView = category;
			});
		}

		public LoadWalletViewModel LoadWalletCategory { get; }

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

		public ViewModelBase CurrentView
		{
			get { return _currentView; }
			set { this.RaiseAndSetIfChanged(ref _currentView, value); }
		}
	}
}
