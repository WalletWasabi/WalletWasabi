﻿using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using System;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class EncryptionManagerViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;

		public EncryptionManagerViewModel() : base("Encryption Manager")
		{
			Categories = new ObservableCollection<CategoryViewModel>
			{
				new SignMessageViewModel(this),
				new VerifyMessageViewModel(this),
				new EncryptMessageViewModel(this),
				new DecryptMessageViewModel(this),
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

		public void SelectSignMessage()
		{
			SelectedCategory = Categories.First(x => x is SignMessageViewModel);
		}

		public ViewModelBase CurrentView
		{
			get { return _currentView; }
			set { this.RaiseAndSetIfChanged(ref _currentView, value); }
		}
	}
}
