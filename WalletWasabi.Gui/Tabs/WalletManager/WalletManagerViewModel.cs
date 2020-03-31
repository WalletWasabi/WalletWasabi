using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Disposables;
using WalletWasabi.Blockchain.Keys;
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

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}

		private LoadWalletViewModel LoadWalletDesktop { get; set; }
		public LoadWalletViewModel LoadWalletPassword { get; private set; }

		public void SelectGenerateWallet()
		{
			SelectedCategory = Categories.First(x => x is GenerateWalletViewModel);
		}

		public void SelectRecoverWallet()
		{
			SelectedCategory = Categories.First(x => x is RecoverWalletViewModel);
		}

		public void SelectLoadWallet(KeyManager keymanager = null)
		{
			SelectedCategory = LoadWalletDesktop;
			LoadWalletDesktop.SelectWallet(keymanager);
		}

		public void SelectTestPassword(string walletname)
		{
			SelectedCategory = LoadWalletPassword;
			LoadWalletPassword.SelectWallet(walletname);
		}

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);

			LoadWalletDesktop = new LoadWalletViewModel(this, LoadWalletType.Desktop);
			LoadWalletPassword = new LoadWalletViewModel(this, LoadWalletType.Password);

			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this),
				new RecoverWalletViewModel(this),
				LoadWalletDesktop,
				LoadWalletPassword,
				new ConnectHardwareWalletViewModel(this)
			};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory)
				.Subscribe(category =>
				{
					category?.OnCategorySelected();
					CurrentView = category;
				});
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
