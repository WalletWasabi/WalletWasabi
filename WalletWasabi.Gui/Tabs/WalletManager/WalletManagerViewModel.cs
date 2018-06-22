using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;

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
				new GenerateWalletViewModel()
			};

			SelectedCategory = Categories.FirstOrDefault();
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
