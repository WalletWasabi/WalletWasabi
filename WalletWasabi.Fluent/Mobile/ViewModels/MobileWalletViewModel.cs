using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>A view-lifetime projection of the existing wallet, never a second wallet implementation.</summary>
public sealed class MobileWalletViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _lifetime = new();
	private readonly SerialDisposable _subscriptions = new();
	private readonly SerialDisposable _coinSubscriptions = new();
	private string _section = "home";
	private string _query = "";
	private string _filter = "All";
	private string _message = "";
	private long _balanceSatoshis;
	private decimal _usdRate;
	private int _privacyPercent;
	private bool _discreet;
	private bool _busy;
	private MobileTransactionItem? _transaction;
	private IReadOnlyList<double> _balanceHistory = Array.Empty<double>();
	private TransactionModel[] _history = Array.Empty<TransactionModel>();

	public MobileWalletViewModel(WalletViewModel wallet)
	{
		Wallet = wallet;
		_discreet = wallet.UiContext.ApplicationSettings.PrivacyMode;
		_usdRate = wallet.WalletModel.AmountProvider.UsdExchangeRate;
		_lifetime.Add(_subscriptions);
		_lifetime.Add(_coinSubscriptions);
		NavigateCommand = Own(ReactiveCommand.Create<string>(Navigate));
		ToggleDiscreetCommand = Own(ReactiveCommand.Create(() => wallet.UiContext.MainViewModel.PrivacyMode.Toggle()));
		SelectFilterCommand = Own(ReactiveCommand.Create<string>(filter => Filter = filter));
		var canEdit = this.WhenAnyValue(x => x.SelectionCount, x => x.IsBusy, (count, busy) => count > 0 && !busy);
		ExcludeSelectedCommand = Own(ReactiveCommand.CreateFromTask(() => SetExclusionAsync(true), canEdit));
		IncludeSelectedCommand = Own(ReactiveCommand.CreateFromTask(() => SetExclusionAsync(false), canEdit));
		CopyTransactionIdCommand = Own(ReactiveCommand.CreateFromTask(async () =>
		{
			if (SelectedTransaction is { } tx) await wallet.UiContext.Clipboard.SetTextAsync(tx.Model.Id.ToString());
		}));
		this.WhenAnyValue(x => x.Query, x => x.Filter).Subscribe(_ => RebuildHistory()).DisposeWith(_lifetime);
	}

	public WalletViewModel Wallet { get; }
	public ObservableCollection<MobileTransactionItem> Transactions { get; } = new();
	public ObservableCollection<MobileTransactionItem> RecentTransactions { get; } = new();
	public ObservableCollection<MobileCoinItem> Coins { get; } = new();
	public IReadOnlyList<string> Filters { get; } = new[] { "All", "Received", "Sent", "CoinJoin" };
	public ICommand NavigateCommand { get; }
	public ICommand ToggleDiscreetCommand { get; }
	public ICommand SelectFilterCommand { get; }
	public ICommand ExcludeSelectedCommand { get; }
	public ICommand IncludeSelectedCommand { get; }
	public ICommand CopyTransactionIdCommand { get; }
	public string Section { get => _section; private set => this.RaiseAndSetIfChanged(ref _section, value); }
	public string Query { get => _query; set => this.RaiseAndSetIfChanged(ref _query, value); }
	public string Filter { get => _filter; set => this.RaiseAndSetIfChanged(ref _filter, value); }
	public string Message { get => _message; private set => this.RaiseAndSetIfChanged(ref _message, value); }
	public bool IsBusy { get => _busy; private set => this.RaiseAndSetIfChanged(ref _busy, value); }
	public MobileTransactionItem? SelectedTransaction { get => _transaction; private set => this.RaiseAndSetIfChanged(ref _transaction, value); }
	public IReadOnlyList<double> BalanceHistory { get => _balanceHistory; private set => this.RaiseAndSetIfChanged(ref _balanceHistory, value); }
	public int PrivacyPercent { get => _privacyPercent; private set => this.RaiseAndSetIfChanged(ref _privacyPercent, value); }
	public string BalanceText => _discreet ? "•••••• BTC" : $"{Money.Satoshis(_balanceSatoshis).ToDecimal(MoneyUnit.BTC):0.########} BTC";
	public string FiatBalanceText => _discreet ? "•••••• USD" : _usdRate > 0 ? $"≈ ${_balanceSatoshis / 100_000_000m * _usdRate:N2} USD" : "Exchange rate unavailable";
	public bool ShowHistoryChart => !_discreet && BalanceHistory.Count > 1;
	public bool IsEmpty => Transactions.Count == 0;
	public int CoinCount => Coins.Count;
	public int PrivateCoinCount => Coins.Count(x => x.Model.IsPrivate);
	public int PendingCoinCount => Coins.Count(x => x.Model.IsCoinJoinInProgress);
	public int ExcludedCoinCount => Coins.Count(x => x.Model.IsExcludedFromCoinJoin);
	public int SelectionCount => Coins.Count(x => x.IsSelected);
	public string SelectionText => $"{SelectionCount} selected · {ExcludedCoinCount} excluded from CoinJoin";
	public string PrivacyDescription => "Share of your balance meeting this wallet's anonymity target. This is not a guarantee of anonymity.";
	public bool HasCoinjoin => Wallet.CoinJoinStateViewModel is not null;
	public string NetworkName => Wallet.WalletModel.Network.Name;

	public void Activate()
	{
		var subscriptions = new CompositeDisposable();
		_subscriptions.Disposable = subscriptions;
		Wallet.WalletModel.Balances.ObserveOn(RxApp.MainThreadScheduler).Subscribe(amount =>
		{
			_balanceSatoshis = amount.Btc.Satoshi;
			NotifyAmounts();
			UpdateChart();
		}).DisposeWith(subscriptions);
		Wallet.WalletModel.AmountProvider.BtcToUsdExchangeRate.ObserveOn(RxApp.MainThreadScheduler).Subscribe(rate =>
		{
			_usdRate = rate; NotifyAmounts(); RebuildHistory();
		}).DisposeWith(subscriptions);
		Wallet.UiContext.ApplicationSettings.WhenAnyValue(x => x.PrivacyMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(hidden =>
		{
			_discreet = hidden; NotifyAmounts(); RebuildHistory();
			foreach (var coin in Coins) coin.Refresh();
		}).DisposeWith(subscriptions);
		Wallet.WalletModel.Privacy.Progress.ObserveOn(RxApp.MainThreadScheduler).Subscribe(value => PrivacyPercent = Math.Clamp(value, 0, 100)).DisposeWith(subscriptions);
		Wallet.WalletModel.Transactions.Cache.Connect().ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
		{
			_history = Wallet.WalletModel.Transactions.Cache.Items.OrderByDescending(x => x.Date).ToArray();
			RebuildHistory(); UpdateChart();
		}).DisposeWith(subscriptions);
		Wallet.WalletModel.Coins.List.Connect().ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => RebuildCoins()).DisposeWith(subscriptions);
	}

	public void Deactivate()
	{
		_subscriptions.Disposable = Disposable.Empty;
		_coinSubscriptions.Disposable = Disposable.Empty;
	}

	private T Own<T>(T command) where T : IDisposable { _lifetime.Add(command); return command; }

	private void NotifyAmounts()
	{
		this.RaisePropertyChanged(nameof(BalanceText));
		this.RaisePropertyChanged(nameof(FiatBalanceText));
		this.RaisePropertyChanged(nameof(ShowHistoryChart));
	}

	public void Navigate(string section)
	{
		if (section is not ("home" or "history" or "privacy" or "coinjoin" or "coins" or "discover" or "transaction")) return;
		Section = section;
		Message = "";
	}

	private void OpenTransaction(MobileTransactionItem transaction)
	{
		SelectedTransaction = transaction;
		Navigate("transaction");
	}

	private void RebuildHistory()
	{
		var selectedId = SelectedTransaction?.Model.Id;
		Transactions.Clear(); RecentTransactions.Clear();
		foreach (var model in _history)
		{
			var row = new MobileTransactionItem(model, _discreet, _usdRate, OpenTransaction);
			if (RecentTransactions.Count < 4) RecentTransactions.Add(row);
			if (model.Id == selectedId) SelectedTransaction = row;
			if (Filter == "CoinJoin" && !model.IsCoinjoin || Filter == "Received" && (model.IsCoinjoin || model.Amount <= Money.Zero) || Filter == "Sent" && (model.IsCoinjoin || model.Amount >= Money.Zero)) continue;
			if (!string.IsNullOrWhiteSpace(Query) && !model.Id.ToString().Contains(Query, StringComparison.OrdinalIgnoreCase) && !model.Labels.ToString().Contains(Query, StringComparison.OrdinalIgnoreCase) && !row.Title.Contains(Query, StringComparison.OrdinalIgnoreCase)) continue;
			Transactions.Add(row);
		}
		this.RaisePropertyChanged(nameof(IsEmpty));
	}

	private void UpdateChart()
	{
		// Reconstruct wallet balance from actual net changes, not a market-price chart.
		var recent = _history.Take(30).ToArray();
		var values = new List<double> { _balanceSatoshis / 100_000_000d };
		var balance = _balanceSatoshis / 100_000_000d;
		foreach (var transaction in recent)
		{
			balance -= (double)transaction.Amount.ToDecimal(MoneyUnit.BTC);
			values.Add(balance);
		}
		values.Reverse(); BalanceHistory = values;
		this.RaisePropertyChanged(nameof(ShowHistoryChart));
	}

	private void RebuildCoins()
	{
		var selected = Coins.Where(x => x.IsSelected).Select(x => x.Outpoint).ToHashSet(StringComparer.Ordinal);
		_coinSubscriptions.Disposable = Disposable.Empty;
		var subscriptions = new CompositeDisposable();
		_coinSubscriptions.Disposable = subscriptions;
		Coins.Clear();
		foreach (var model in Wallet.WalletModel.Coins.List.Items.OrderByDescending(x => x.Amount))
		{
			var row = new MobileCoinItem(model, () => _discreet, OnSelectionChanged);
			row.IsSelected = selected.Contains(row.Outpoint);
			Coins.Add(row);
			model.SubscribeToCoinChanges(subscriptions);
			model.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => { row.Refresh(); NotifyCoins(); }).DisposeWith(subscriptions);
		}
		NotifyCoins();
	}

	private void NotifyCoins()
	{
		this.RaisePropertyChanged(nameof(CoinCount)); this.RaisePropertyChanged(nameof(PrivateCoinCount));
		this.RaisePropertyChanged(nameof(PendingCoinCount)); this.RaisePropertyChanged(nameof(ExcludedCoinCount));
		OnSelectionChanged();
		var total = Coins.Sum(x => x.Model.Amount.Satoshi);
		if (total > 0) PrivacyPercent = (int)(Coins.Where(x => x.Model.IsPrivate).Sum(x => x.Model.Amount.Satoshi) * 100m / total);
		else PrivacyPercent = 0;
	}

	private void OnSelectionChanged()
	{
		this.RaisePropertyChanged(nameof(SelectionCount)); this.RaisePropertyChanged(nameof(SelectionText));
	}

	private async Task SetExclusionAsync(bool exclude)
	{
		if (IsBusy) return;
		var selected = Coins.Where(x => x.IsSelected).Select(x => x.Model).ToArray();
		if (selected.Length == 0) return;
		if (selected.Any(x => x.IsCoinJoinInProgress)) { Message = "Wait for the active CoinJoin before changing these coins."; return; }
		IsBusy = true;
		try
		{
			var selectedIds = selected.Select(x => x.GetSmartCoin().Outpoint).ToHashSet();
			var excluded = Wallet.WalletModel.Coins.List.Items.Where(x => selectedIds.Contains(x.GetSmartCoin().Outpoint) ? exclude : x.IsExcludedFromCoinJoin).ToArray();
			await Wallet.WalletModel.Coins.UpdateExcludedCoinsFromCoinjoinAsync(excluded);
			Message = exclude ? "Selected coins excluded from CoinJoin. They are not frozen from spending." : "Selected coins included in CoinJoin eligibility.";
			RebuildCoins();
		}
		catch (Exception) { Message = "Could not save CoinJoin exclusions. The operation was not confirmed; refresh and try again."; }
		finally { IsBusy = false; }
	}

	public void Dispose() => _lifetime.Dispose();
}

public sealed class MobileTransactionItem
{
	public MobileTransactionItem(TransactionModel model, bool hidden, decimal rate, Action<MobileTransactionItem> open)
	{
		Model = model;
		Title = model.IsCoinjoin ? "CoinJoin" : model.Amount < Money.Zero ? "Sent" : "Received";
		Icon = model.IsCoinjoin ? "coinjoin" : model.Amount < Money.Zero ? "send" : "receive";
		var btc = model.Amount.ToDecimal(MoneyUnit.BTC);
		AmountText = hidden ? "•••••• BTC" : $"{(btc > 0 ? "+" : "")}{btc:0.########} BTC";
		FiatText = hidden ? "•••••• USD" : rate > 0 ? $"${Math.Abs(btc) * rate:N2} USD" : "";
		Labels = hidden ? "Hidden in discreet mode" : model.Labels.ToString();
		Id = hidden ? "Hidden in discreet mode" : model.Id.ToString();
		Date = model.Date.ToLocalTime().ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture);
		Status = model.IsConfirmed ? $"{model.Confirmations} confirmations" : model.Status.ToString();
		Fee = hidden ? "•••••• BTC" : model.Fee is { } fee ? $"{fee.ToDecimal(MoneyUnit.BTC):0.########} BTC" : "Unknown";
		FeeRate = model.FeeRate is { } feeRate ? $"{feeRate.SatoshiPerByte:0.###} sat/vB" : "Unknown";
		Block = model.BlockHeight > 0 ? model.BlockHeight.ToString(CultureInfo.InvariantCulture) : "Unconfirmed";
		OpenCommand = new MobileActionCommand(() => open(this));
	}
	public TransactionModel Model { get; }
	public string Title { get; }
	public string Icon { get; }
	public string AmountText { get; }
	public string FiatText { get; }
	public string Labels { get; }
	public string Id { get; }
	public string Date { get; }
	public string Status { get; }
	public string Fee { get; }
	public string FeeRate { get; }
	public string Block { get; }
	public bool IsIncoming => !Model.IsCoinjoin && Model.Amount > Money.Zero;
	public ICommand OpenCommand { get; }
}

public sealed class MobileCoinItem : ReactiveObject
{
	private readonly Func<bool> _hidden;
	private readonly Action _selectionChanged;
	private bool _selected;
	public MobileCoinItem(CoinModel model, Func<bool> hidden, Action selectionChanged) { Model = model; _hidden = hidden; _selectionChanged = selectionChanged; }
	public CoinModel Model { get; }
	public string Outpoint => Model.GetSmartCoin().Outpoint.ToString();
	public string Address => _hidden() ? "Hidden in discreet mode" : Model.BtcAddress ?? Outpoint;
	public string AmountText => _hidden() ? "•••••• BTC" : $"{Model.Amount.ToDecimal(MoneyUnit.BTC):0.########} BTC";
	public string Privacy => Model.IsPrivate ? "Private" : Model.IsSemiPrivate ? "Semi-private" : "Non-private";
	public string Status => Model.IsCoinJoinInProgress ? "CoinJoin in progress" : Model.IsExcludedFromCoinJoin ? "Excluded from CoinJoin" : Model.IsConfirmed ? "Confirmed" : "Unconfirmed";
	public bool IsSelected { get => _selected; set { this.RaiseAndSetIfChanged(ref _selected, value); _selectionChanged(); } }
	public void Refresh() { this.RaisePropertyChanged(nameof(Address)); this.RaisePropertyChanged(nameof(AmountText)); this.RaisePropertyChanged(nameof(Privacy)); this.RaisePropertyChanged(nameof(Status)); }
}

/// <summary>Stateless row command; no subscriptions and no captured application services.</summary>
internal sealed class MobileActionCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
