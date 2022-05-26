using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class WalletBalanceTileViewModel : TileViewModel
{
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _balanceChanged;
	private readonly ObservableCollection<HistoryItemViewModelBase> _history;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceBtc;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceFiat;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balancePrivateBtc;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceNonPrivateBtc;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _recentTransactionName;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _recentTransactionDate;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _recentTransactionStatus;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _showRecentTransaction;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _hasBalance;
	[AutoNotify] private double _percentPrivate;

	public WalletBalanceTileViewModel(Wallet wallet, IObservable<Unit> balanceChanged, ObservableCollection<HistoryItemViewModelBase> history)
	{
		_wallet = wallet;
		_balanceChanged = balanceChanged;
		_history = history;
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_balanceChanged
			.Subscribe(_ => UpdateBalance())
			.DisposeWith(disposables);

		_history.ToObservableChangeSet()
			.Throttle(TimeSpan.FromMilliseconds(50))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => UpdateRecentTransaction())
			.DisposeWith(disposables);
	}

	private void UpdateBalance()
	{
		var totalAmount = _wallet.Coins.TotalAmount();

		BalanceBtc = $"{totalAmount.ToFormattedString()} BTC";

		var fiatAmount = _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC) * _wallet.Synchronizer.UsdExchangeRate;
		var fiatFormat =
			fiatAmount >= 10
			? "N0"
			: "N2";

		BalanceFiat = fiatAmount.GenerateFiatText("USD", fiatFormat);

		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;
		var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold);
		var normalCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold);

		var privateDecimalAmount = privateCoins.TotalAmount();
		var totalDecimalAmount = _wallet.Coins.TotalAmount();

		HasBalance = totalAmount > Money.Zero;

		BalancePrivateBtc = privateDecimalAmount
			.FormattedBtc() + " BTC";

		BalanceNonPrivateBtc = normalCoins.TotalAmount().ToDecimal(MoneyUnit.BTC)
			.FormattedBtc() + " BTC";

		PercentPrivate = totalDecimalAmount.ToDecimal(MoneyUnit.BTC) == 0M ? 0d : (double)(privateDecimalAmount.ToDecimal(MoneyUnit.BTC) / totalDecimalAmount.ToDecimal(MoneyUnit.BTC));
	}

	private void UpdateRecentTransaction()
	{
		var recent = _history.FirstOrDefault();
		if (recent is { })
		{
			var isIncoming = recent.IncomingAmount is { };

			RecentTransactionName = isIncoming ? "Incoming" : "Outgoing";
			RecentTransactionDate = recent.DateString;
			RecentTransactionStatus = $"{(isIncoming ? recent.IncomingAmount : recent.OutgoingAmount)} BTC - {(recent.IsConfirmed ? "Confirmed" : "Pending")}";

			ShowRecentTransaction = true;
		}
		else
		{
			RecentTransactionName = default;
			RecentTransactionDate = default;
			RecentTransactionStatus = default;
			ShowRecentTransaction = false;
		}
	}
}
