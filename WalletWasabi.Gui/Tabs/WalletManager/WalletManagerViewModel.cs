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

		public WalletManagerViewModel() : base("Wallet Manager")
		{
			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(),
				new RecoverWalletViewModel()
			};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
			{
				category?.OnCategorySelected();
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
	}
}
