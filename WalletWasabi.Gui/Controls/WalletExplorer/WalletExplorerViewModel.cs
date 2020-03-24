using System;
using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[Export]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IActivatableExtension
	{
		private ObservableCollection<WalletViewModelBase> _wallets;
		private ViewModelBase _selectedItem;
		private Dictionary<Wallet, WalletViewModelBase> _walletDictionary;

		public override Location DefaultLocation => Location.Right;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModelBase>();

			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();

			WalletManager = Locator.Current.GetService<Global>().WalletManager;

			Observable.FromEventPattern<WalletState>(WalletManager, nameof(WalletManager.WalletStateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					var wallet = x.Sender as Wallet;

					if (wallet is { } && _walletDictionary.ContainsKey(wallet))
					{
						if (_walletDictionary[wallet] is ClosedWalletViewModel cwvm && x.EventArgs == WalletState.Started)
						{
							OpenClosedWallet(cwvm);
						}
					}
				});
		}

		private WalletManager WalletManager { get; }

		public ObservableCollection<WalletViewModelBase> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		public ViewModelBase SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		private void InsertWallet(WalletViewModelBase walletVM)
		{
			Wallets.InsertSorted(walletVM);
			_walletDictionary.Add(walletVM.Wallet, walletVM);
		}

		internal WalletViewModelBase OpenWallet(Wallet wallet)
		{
			if (wallet.Coins.Any())
			{
				// If already have coins then open the last active tab first.
				return OpenWallet(wallet, receiveDominant: false);
			}
			else // Else open with Receive tab first.
			{
				return OpenWallet(wallet, receiveDominant: true);
			}
		}

		private void OpenClosedWallet(ClosedWalletViewModel closedWalletViewModel)
		{
			var select = SelectedItem == closedWalletViewModel;

			RemoveWallet(closedWalletViewModel);

			var walletViewModel = OpenWallet(closedWalletViewModel.Wallet);

			if (select)
			{
				SelectedItem = walletViewModel;
			}
		}

		private WalletViewModelBase OpenWallet(Wallet wallet, bool receiveDominant, bool select = true)
		{
			if (_wallets.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				throw new System.Exception("Wallet already opened.");
			}

			var walletViewModel = new WalletViewModel(wallet);

			InsertWallet(walletViewModel);

			walletViewModel.OpenWallet(receiveDominant);

			return walletViewModel;
		}

		internal void RemoveWallet(WalletViewModelBase walletVM)
		{
			walletVM.Dispose();

			Wallets.Remove(walletVM);
			_walletDictionary.Remove(walletVM.Wallet);
		}

		private void LoadWallets()
		{
			foreach (var wallet in WalletManager.GetKeyManagers())
			{
				InsertWallet(new ClosedWalletViewModel(WalletManager.GetWalletByName(wallet.WalletName)));
			}
		}

		public void BeforeActivation()
		{
		}

		public void Activation()
		{
			IoC.Get<IShell>().MainPerspective.AddOrSelectTool(this);

			LoadWallets();
		}
	}
}
