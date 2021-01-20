using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class WalletManagerViewModel : ViewModelBase
	{
		private readonly Dictionary<Wallet, WalletViewModelBase> _walletDictionary;
		[AutoNotify] private WalletViewModelBase? _selectedItem;
		[AutoNotify] private ObservableCollection<WalletViewModelBase> _items;
		[AutoNotify] private bool _anyWalletStarted;

		public WalletManagerViewModel(WalletManager walletManager, UiConfig uiConfig, LegalChecker legalChecker)
		{
			Model = walletManager;
			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();
			_items = new ObservableCollection<WalletViewModelBase>();

			Observable
				.FromEventPattern<WalletState>(walletManager, nameof(WalletManager.WalletStateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					x =>
				{
					var wallet = x.Sender as Wallet;

					if (wallet is { } && _walletDictionary.ContainsKey(wallet))
					{
						if (wallet.State == WalletState.Stopping)
						{
							RemoveWallet(_walletDictionary[wallet]);
						}
						else if (_walletDictionary[wallet] is ClosedWalletViewModel cwvm && wallet.State == WalletState.Started)
						{
							OpenClosedWallet(walletManager, uiConfig, cwvm, legalChecker);
						}
					}

					AnyWalletStarted = Items.Any(y => y.WalletState == WalletState.Started);
				});

			Observable
				.FromEventPattern<Wallet>(walletManager, nameof(WalletManager.WalletAdded))
				.Select(x => x.EventArgs)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					wallet =>
				{
					WalletViewModelBase vm = (wallet.State <= WalletState.Starting)
						? ClosedWalletViewModel.Create(walletManager, wallet, legalChecker)
						: WalletViewModel.Create(uiConfig, wallet, legalChecker);

					InsertWallet(vm);
				});

			Dispatcher.UIThread.Post(() => LoadWallets(walletManager, legalChecker));
		}

		public WalletManager Model { get; }

		private void OpenClosedWallet(WalletManager walletManager, UiConfig uiConfig, ClosedWalletViewModel closedWalletViewModel, LegalChecker legalChecker)
		{
			var select = SelectedItem == closedWalletViewModel;

			RemoveWallet(closedWalletViewModel);

			var walletViewModel = OpenWallet(walletManager, uiConfig, closedWalletViewModel.Wallet, legalChecker);

			if (select)
			{
				SelectedItem = walletViewModel;
			}
		}

		private WalletViewModelBase OpenWallet(WalletManager walletManager, UiConfig uiConfig, Wallet wallet, LegalChecker legalChecker)
		{
			if (_items.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				throw new Exception("Wallet already opened.");
			}

			var walletViewModel = WalletViewModel.Create(uiConfig, wallet, legalChecker);

			InsertWallet(walletViewModel);

			if (!walletManager.AnyWallet(x => x.State >= WalletState.Started && x != walletViewModel.Wallet))
			{
				walletViewModel.OpenWalletTabs();
			}

			walletViewModel.IsExpanded = true;

			return walletViewModel;
		}

		private void InsertWallet(WalletViewModelBase wallet)
		{
			Items.InsertSorted(wallet);
			_walletDictionary.Add(wallet.Wallet, wallet);
		}

		private void RemoveWallet(WalletViewModelBase wallet)
		{
			wallet.Dispose();

			_items.Remove(wallet);
			_walletDictionary.Remove(wallet.Wallet);
		}

		private void LoadWallets(WalletManager walletManager, LegalChecker legalChecker)
		{
			foreach (var wallet in walletManager.GetWallets())
			{
				InsertWallet(ClosedWalletViewModel.Create(walletManager, wallet, legalChecker));
			}
		}
	}
}
