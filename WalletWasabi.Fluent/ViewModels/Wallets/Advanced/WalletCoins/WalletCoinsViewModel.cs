using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

[NavigationMetaData(Title = "Wallet Coins")]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly WalletViewModel _walletViewModel;
	private readonly IObservable<Unit> _balanceChanged;

	private readonly ReadOnlyObservableCollection<WalletCoinViewModel> _coins;
	private readonly SourceList<WalletCoinViewModel> _confirmationWordsSourceList = new();

	public WalletCoinsViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		SetupCancel(false, true, true);
		NextCommand = CancelCommand;
		_walletViewModel = walletViewModel;
		_balanceChanged = balanceChanged;

		_confirmationWordsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out _coins)
			.Subscribe();
	}

	public ReadOnlyObservableCollection<WalletCoinViewModel> Coins => _coins;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.Merge(
			_balanceChanged.Select(_ => Unit.Default),
			_walletViewModel.WhenAnyValue(w => w.IsCoinJoining).Select(_ => Unit.Default))
			.Subscribe(_ =>
			{
				Update();
			});

		disposables.Add(_confirmationWordsSourceList);
	}

	private void Update()
	{
		var coins = _walletViewModel.Wallet.Coins.Select(c => new WalletCoinViewModel(c));

		_confirmationWordsSourceList.Clear();
		_confirmationWordsSourceList.AddRange(coins);
	}
}
