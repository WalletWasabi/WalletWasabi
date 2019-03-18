using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class WalletManagerViewModel : WasabiDocumentTabViewModel, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;

		public WalletManagerViewModel() : base("Wallet Manager")
		{
			Disposables = new CompositeDisposable();

			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this).DisposeWith(Disposables),
				new RecoverWalletViewModel(this).DisposeWith(Disposables),
				new LoadWalletViewModel(this, false).DisposeWith(Disposables),
				new LoadWalletViewModel(this, true).DisposeWith(Disposables)
			};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
			{
				category?.OnCategorySelected();

				CurrentView = category;
			}).DisposeWith(Disposables);
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
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && !((LoadWalletViewModel)x).RequirePassword);
		}

		public void SelectTestPassword()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && ((LoadWalletViewModel)x).RequirePassword);
		}

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
