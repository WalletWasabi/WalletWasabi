using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class WalletBalanceTileViewModel : TileViewModel
{
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _balanceChanged;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceBtc;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private decimal _balanceFiat;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _hasBalance;

	public WalletBalanceTileViewModel(WalletViewModel walletVm)
	{
		_wallet = walletVm.Wallet;
		_balanceChanged = walletVm.UiTriggers.BalanceUpdateTrigger;
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_balanceChanged
			.Subscribe(_ => UpdateBalance())
			.DisposeWith(disposables);
	}

	private void UpdateBalance()
	{
		var totalAmount = _wallet.Coins.TotalAmount();

		BalanceBtc = $"{totalAmount.ToFormattedString()} BTC";

		BalanceFiat = totalAmount.BtcToUsd(_wallet.Synchronizer.UsdExchangeRate);

		HasBalance = totalAmount > Money.Zero;
	}
}
