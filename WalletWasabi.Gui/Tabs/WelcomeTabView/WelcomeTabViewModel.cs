using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Tabs.WelcomeTab.GenerateWallets;
using WalletWasabi.Gui.Tabs.WelcomeTab.HardwareWallets;
using WalletWasabi.Gui.Tabs.WelcomeTab.LoadWallets;
using WalletWasabi.Gui.Tabs.WelcomeTab.RecoverWallets;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Tabs.WelcomeTab
{
	[Export]
	[Shared]
	public class WelcomeTabViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;

		public WelcomeTabViewModel() : base("Welcome!")
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


		public class DummyNewsItem
		{
			private static Random RNG = new Random();
			private static DateTime GetRandomDate()
			{
				var b = DateTime.Now;
				return new DateTime(b.Year, b.Month, b.Day - RNG.Next(0, 7));
			}

			public DummyNewsItem()
			{ 
				PublishDate = GetRandomDate();
			}

			public string Title { get; } = "Lorem Ipsum";
			public string Content { get; } = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
			public DateTime PublishDate { get; }
		}

#pragma warning disable
		public List<DummyNewsItem> NewsItems { get; set; } = new List<DummyNewsItem>()
		{
			new DummyNewsItem(),
			new DummyNewsItem(),
			new DummyNewsItem(),
			new DummyNewsItem(),
			new DummyNewsItem(),
		}.OrderByDescending(x => x.PublishDate).ToList();
#pragma warning restore

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

			var global = Locator.Current.GetService<Global>();
			var walletManager = global.WalletManager;

			if (!walletManager.GetWallets().Any(wallet => wallet.State == WalletState.Started))
			{
				// If there aren't any opened wallet then close this walletmanager if the first wallet loaded.
				Observable
					.FromEventPattern<WalletState>(walletManager, nameof(walletManager.WalletStateChanged))
					.Select(x => x.EventArgs)
					.Where(x => x == WalletState.Started)
					.Take(1)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(wallet =>
					{
						// Only close this tab if the user still looking at the load tab.
						if (CurrentView == LoadWalletDesktop)
						{
							OnClose();
						}
					}).DisposeWith(disposables);
			}
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
