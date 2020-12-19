using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
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
			ObservableCollection<NavBarItemViewModel> bottomItems)
			: base(walletManager)
		{
			Wallets = wallets;
			BottomItems = bottomItems;

			this.WhenAnyValue(x => x.SelectedBottomItem)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					x?.OpenCommand.Execute(null);
				});
		}

		public ObservableCollection<WalletViewModelBase> Wallets { get; }
		public ObservableCollection<NavBarItemViewModel> BottomItems { get; }

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