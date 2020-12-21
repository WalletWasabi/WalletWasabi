using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Search;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public partial class FullScreenLoginViewModel : LoginViewModelBase
	{
		[AutoNotify] private NavBarItemViewModel? _selectedBottomItem;

		public FullScreenLoginViewModel(
			WalletManager walletManager,
			ObservableCollection<WalletViewModelBase> wallets,
			SearchPageViewModel actionCenter,
			AddWalletPageViewModel addWallet)
			: base(walletManager)
		{
			Wallets = wallets;
			ActionCenter = actionCenter;
			AddWallet = addWallet;
		}

		public ObservableCollection<WalletViewModelBase> Wallets { get; }
		public SearchPageViewModel ActionCenter { get; }
		public AddWalletPageViewModel AddWallet { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			Observable
				.FromEventPattern(Wallets, nameof(Wallets.CollectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SelectedWallet = Wallets.FirstOrDefault())
				.DisposeWith(disposable);
		}
	}
}