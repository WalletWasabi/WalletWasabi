using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>A view-lifetime projection of the existing wallet, never a second wallet implementation.</summary>
public sealed class MobileWalletViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _lifetime = new();
	private readonly SerialDisposable _subscriptions = new();
	private readonly SerialDisposable _coinSubscriptions = new();
	private readonly Dictionary<uint256, MobileTransactionItem> _transactionRows = new();
	private readonly Dictionary<string, MobileCoinItem> _coinRows = new(StringComparer.Ordinal);
	private string _section = "home";
	private string _query = "";
	private string _filter = "All";
	private string _message = "";
	private long _balanceSatoshis;
	private decimal _usdRate;
	private int _privacyPercent;
	private bool _discreet;
	private bool _busy;
	private bool _disposed;
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
		ToggleDiscreetCommand = Own(ReactiveCommand.Create(() =>
		{
			if (wallet.UiContext.MainViewModel is { } main) main.PrivacyMode.Toggle();
			else wallet.UiContext.ApplicationSettings.PrivacyMode = !wallet.UiContext.ApplicationSettings.PrivacyMode;
		}));
		SwitchWalletCommand = Own(ReactiveCommand.Create(() => wallet.UiContext.Navigate().To(new MobileWalletsListViewModel(wallet.UiContext), NavigationTarget.HomeScreen, NavigationMode.Clear)));
		SelectFilterCommand = Own(ReactiveCommand.Create<string>(filter => Filter = filter));
		var canEdit = this.WhenAnyValue(x => x.SelectionCount, x => x.IsBusy, (count, busy) => count > 0 && !busy);
		ExcludeSelectedCommand = Own(ReactiveCommand.CreateFromTask(() => SetExclusionAsync(true), canEdit));
		IncludeSelectedCommand = Own(ReactiveCommand.CreateFromTask(() => SetExclusionAsync(false), canEdit));
		CopyTransactionIdCommand = Own(ReactiveCommand.CreateFromTask(async () =>
		{
			if (SelectedTransaction is not { } tx) return;
			try { await wallet.UiContext.Clipboard.SetTextAsync(tx.Model.Id.ToString()); if (!_disposed) Message = "Transaction ID copied."; }
			catch (Exception) { if (!_disposed) Message = "Could not access the clipboard."; }
		}));
		OpenExplorerCommand = Own(ReactiveCommand.CreateFromTask(async () =>
		{
			if (SelectedTransaction is not { } tx) return;
			if (wallet.WalletModel.Network != Network.Main) { Message = "The public explorer shortcut is available only for Bitcoin mainnet."; return; }
			try { await wallet.UiContext.FileSystem.OpenBrowserAsync($"https://mempool.space/tx/{tx.Model.Id}"); }
			catch (Exception) { if (!_disposed) Message = "Could not open the external browser."; }
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
	public ICommand SwitchWalletCommand { get; }
	public ICommand SelectFilterCommand { get; }
	public ICommand ExcludeSelectedCommand { get; }
	public ICommand IncludeSelectedCommand { get; }
	public ICommand CopyTransactionIdCommand { get; }
	public ICommand OpenExplorerCommand { get; }
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
		ObjectDisposedException.ThrowIf(_disposed, this);
		var subscriptions = new CompositeDisposable();
		_subscriptions.Disposable = subscriptions;
		Wallet.WalletModel.Balances.ObserveOn(RxApp.MainThreadScheduler).Subscribe(amount => { _balanceSatoshis = amount.Btc.Satoshi; NotifyAmounts(); UpdateChart(); }).DisposeWith(subscriptions);
		Wallet.WalletModel.AmountProvider.BtcToUsdExchangeRate.ObserveOn(RxApp.MainThreadScheduler).Subscribe(rate => { _usdRate = rate; NotifyAmounts(); RebuildHistory(); }).DisposeWith(subscriptions);
		Wallet.UiContext.ApplicationSettings.WhenAnyValue(x => x.PrivacyMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(hidden =>
		{
			_discreet = hidden;
			NotifyAmounts();
			RebuildHistory();
			foreach (var coin in Coins) coin.Refresh();
		}).DisposeWith(subscriptions);
		Wallet.WalletModel.Privacy.Progress.ObserveOn(RxApp.MainThreadScheduler).Subscribe(value => PrivacyPercent = Math.Clamp(value, 0, 100)).DisposeWith(subscriptions);
		Wallet.WalletModel.Transactions.Cache.Connect().ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
		{
			_history = Wallet.WalletModel.Transactions.Cache.Items.SelectMany(IndividualTransactions).GroupBy(x => x.Id).Select(x => x.First()).OrderByDescending(x => x.Date).ToArray();
			RebuildHistory();
			UpdateChart();
		}).DisposeWith(subscriptions);
		Wallet.WalletModel.Coins.List.Connect().ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => RebuildCoins()).DisposeWith(subscriptions);
	}

	private static IEnumerable<TransactionModel> IndividualTransactions(TransactionModel transaction)
	{
		if (transaction.IsCoinjoinGroup)
		{
			foreach (var child in transaction.Children)
				foreach (var individual in IndividualTransactions(child))
					yield return individual;
		}
		else yield return transaction;
	}

	public void Deactivate() { _subscriptions.Disposable = Disposable.Empty; _coinSubscriptions.Disposable = Disposable.Empty; }
	private T Own<T>(T command) where T : IDisposable { _lifetime.Add(command); return command; }
	private void NotifyAmounts() { this.RaisePropertyChanged(nameof(BalanceText)); this.RaisePropertyChanged(nameof(FiatBalanceText)); this.RaisePropertyChanged(nameof(ShowHistoryChart)); }

	public void Navigate(string section)
	{
		if (section is not ("home" or "history" or "privacy" or "coinjoin" or "coins" or "discover" or "transaction")) return;
		if (section == "home") { Query = ""; Filter = "All"; }
		Section = section;
		Message = "";
	}

	private void OpenTransaction(MobileTransactionItem transaction) { SelectedTransaction = transaction; Navigate("transaction"); }

	private void RebuildHistory()
	{
		var present = new HashSet<uint256>();
		var visible = new List<MobileTransactionItem>();
		var recent = new List<MobileTransactionItem>(4);
		foreach (var model in _history)
		{
			present.Add(model.Id);
			if (!_transactionRows.TryGetValue(model.Id, out var row))
			{
				row = new MobileTransactionItem(model, _discreet, _usdRate, OpenTransaction);
				_transactionRows.Add(model.Id, row);
			}
			else row.Update(model, _discreet, _usdRate);
			if (recent.Count < 4) recent.Add(row);
			if (Filter == "CoinJoin" && !model.IsCoinjoin || Filter == "Received" && (model.IsCoinjoin || model.Amount <= Money.Zero) || Filter == "Sent" && (model.IsCoinjoin || model.Amount >= Money.Zero)) continue;
			if (!string.IsNullOrWhiteSpace(Query) && !model.Id.ToString().Contains(Query, StringComparison.OrdinalIgnoreCase) && !model.Labels.ToString().Contains(Query, StringComparison.OrdinalIgnoreCase) && !row.Title.Contains(Query, StringComparison.OrdinalIgnoreCase)) continue;
			visible.Add(row);
		}
		foreach (var removed in _transactionRows.Keys.Where(id => !present.Contains(id)).ToArray()) _transactionRows.Remove(removed);
		MobileCollectionSync.Reconcile(RecentTransactions, recent);
		MobileCollectionSync.Reconcile(Transactions, visible);
		if (SelectedTransaction is { } selected && !present.Contains(selected.Model.Id))
		{
			SelectedTransaction = null;
			if (Section == "transaction") { Section = "history"; Message = "This transaction is no longer present in the wallet history."; }
		}
		this.RaisePropertyChanged(nameof(IsEmpty));
	}

	private void UpdateChart()
	{
		var values = new List<double> { _balanceSatoshis / 100_000_000d };
		var balance = _balanceSatoshis / 100_000_000d;
		foreach (var transaction in _history.Take(30)) { balance -= (double)transaction.Amount.ToDecimal(MoneyUnit.BTC); values.Add(balance); }
		values.Reverse();
		if (BalanceHistory.SequenceEqual(values)) return;
		BalanceHistory = values;
		this.RaisePropertyChanged(nameof(ShowHistoryChart));
	}

	private void RebuildCoins()
	{
		_coinSubscriptions.Disposable = Disposable.Empty;
		var subscriptions = new CompositeDisposable();
		_coinSubscriptions.Disposable = subscriptions;
		var present = new HashSet<string>(StringComparer.Ordinal);
		var desired = new List<MobileCoinItem>();
		foreach (var model in Wallet.WalletModel.Coins.List.Items.OrderByDescending(x => x.Amount))
		{
			var id = model.GetSmartCoin().Outpoint.ToString();
			present.Add(id);
			if (!_coinRows.TryGetValue(id, out var row))
			{
				row = new MobileCoinItem(model, () => _discreet, OnSelectionChanged);
				_coinRows.Add(id, row);
			}
			else row.ReplaceModel(model);
			desired.Add(row);
		}
		foreach (var removed in _coinRows.Keys.Where(id => !present.Contains(id)).ToArray()) _coinRows.Remove(removed);
		MobileCollectionSync.Reconcile(Coins, desired);
		foreach (var row in desired)
		{
			row.Model.SubscribeToCoinChanges(subscriptions);
			row.Model.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => { row.Refresh(); NotifyCoins(); }).DisposeWith(subscriptions);
		}
		NotifyCoins();
	}

	private void NotifyCoins()
	{
		this.RaisePropertyChanged(nameof(CoinCount));
		this.RaisePropertyChanged(nameof(PrivateCoinCount));
		this.RaisePropertyChanged(nameof(PendingCoinCount));
		this.RaisePropertyChanged(nameof(ExcludedCoinCount));
		OnSelectionChanged();
		var total = Coins.Sum(x => x.Model.Amount.Satoshi);
		PrivacyPercent = total > 0 ? (int)(Coins.Where(x => x.Model.IsPrivate).Sum(x => x.Model.Amount.Satoshi) * 100m / total) : 0;
	}

	private void OnSelectionChanged() { this.RaisePropertyChanged(nameof(SelectionCount)); this.RaisePropertyChanged(nameof(SelectionText)); }

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
			if (_disposed) return;
			Message = exclude ? "Selected coins excluded from CoinJoin. They are not frozen from spending." : "Selected coins included in CoinJoin eligibility.";
			RebuildCoins();
		}
		catch (Exception) { if (!_disposed) Message = "Could not confirm saving CoinJoin exclusions. Refresh and try again."; }
		finally { if (!_disposed) IsBusy = false; }
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_lifetime.Dispose();
		_transactionRows.Clear();
		_coinRows.Clear();
	}
}

public sealed class MobileTransactionItem : ReactiveObject
{
	private TransactionModel _model;
	private bool _hidden;
	private decimal _rate;
	private DisplayState _displayState;
	private static readonly string[] DisplayProperties =
	{
		nameof(Model), nameof(Title), nameof(Icon), nameof(AmountText), nameof(FiatText), nameof(Labels),
		nameof(Id), nameof(Date), nameof(Status), nameof(Fee), nameof(FeeRate), nameof(Block), nameof(IsIncoming)
	};

	public MobileTransactionItem(TransactionModel model, bool hidden, decimal rate, Action<MobileTransactionItem> open)
	{
		_model = model;
		Update(model, hidden, rate);
		OpenCommand = new MobileActionCommand(() => open(this));
	}

	public TransactionModel Model => _model;
	public string Title => Model.IsCoinjoin ? "CoinJoin" : Model.Amount < Money.Zero ? "Sent" : "Received";
	public string Icon => Model.IsCoinjoin ? "coinjoin" : Model.Amount < Money.Zero ? "send" : "receive";
	public string AmountText => _hidden ? "•••••• BTC" : $"{(Model.Amount > Money.Zero ? "+" : "")}{Model.Amount.ToDecimal(MoneyUnit.BTC):0.########} BTC";
	public string FiatText => _hidden ? "•••••• USD" : _rate > 0 ? $"${Math.Abs(Model.Amount.ToDecimal(MoneyUnit.BTC)) * _rate:N2} USD" : "";
	public string Labels => _hidden ? "Hidden in discreet mode" : Model.Labels.ToString();
	public string Id => _hidden ? "Hidden in discreet mode" : Model.Id.ToString();
	public string Date => Model.Date.ToLocalTime().ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture);
	public string Status => Model.IsConfirmed ? $"{Model.Confirmations} confirmations" : Model.Status.ToString();
	public string Fee => _hidden ? "•••••• BTC" : Model.Fee is { } fee ? $"{fee.ToDecimal(MoneyUnit.BTC):0.########} BTC" : "Unknown";
	public string FeeRate => Model.FeeRate is { } feeRate ? $"{feeRate.SatoshiPerByte:0.###} sat/vB" : "Unknown";
	public string Block => Model.BlockHeight > 0 ? Model.BlockHeight.ToString(CultureInfo.InvariantCulture) : "Unconfirmed";
	public bool IsIncoming => !Model.IsCoinjoin && Model.Amount > Money.Zero;
	public ICommand OpenCommand { get; }

	public void Update(TransactionModel model, bool hidden, decimal rate)
	{
		var next = new DisplayState(model.Id, model.Amount.Satoshi, model.Fee?.Satoshi, model.FeeRate?.SatoshiPerByte,
			model.Date, model.Labels.ToString(), model.Confirmations, model.BlockHeight, model.IsConfirmed, model.IsCoinjoin, model.Status.ToString(), hidden, rate);
		_model = model;
		_hidden = hidden;
		_rate = rate;
		if (next == _displayState) return;
		_displayState = next;
		foreach (var property in DisplayProperties) this.RaisePropertyChanged(property);
	}

	private readonly record struct DisplayState(uint256 Id, long Satoshis, long? FeeSatoshis, decimal? FeeRate,
		DateTimeOffset Date, string Labels, uint Confirmations, uint Block, bool Confirmed, bool Coinjoin, string Status, bool Hidden, decimal Rate);
}

public sealed class MobileCoinItem : ReactiveObject
{
	private readonly Func<bool> _hidden;
	private readonly Action _selectionChanged;
	private CoinModel _model;
	private bool _selected;
	public MobileCoinItem(CoinModel model, Func<bool> hidden, Action selectionChanged) { _model = model; _hidden = hidden; _selectionChanged = selectionChanged; }
	public CoinModel Model => _model;
	public string Outpoint => Model.GetSmartCoin().Outpoint.ToString();
	public string Address => _hidden() ? "Hidden in discreet mode" : Model.BtcAddress ?? Outpoint;
	public string AmountText => _hidden() ? "•••••• BTC" : $"{Model.Amount.ToDecimal(MoneyUnit.BTC):0.########} BTC";
	public string Privacy => Model.IsPrivate ? "Private" : Model.IsSemiPrivate ? "Semi-private" : "Non-private";
	public string Status => Model.IsCoinJoinInProgress ? "CoinJoin in progress" : Model.IsExcludedFromCoinJoin ? "Excluded from CoinJoin" : Model.IsConfirmed ? "Confirmed" : "Unconfirmed";
	public bool IsSelected
	{
		get => _selected;
		set { if (_selected == value) return; this.RaiseAndSetIfChanged(ref _selected, value); _selectionChanged(); }
	}
	public void ReplaceModel(CoinModel model)
	{
		if (model.GetSmartCoin().Outpoint != _model.GetSmartCoin().Outpoint) throw new ArgumentException("A row cannot change its coin identity.", nameof(model));
		_model = model;
		this.RaisePropertyChanged(nameof(Model));
		Refresh();
	}
	public void Refresh() { this.RaisePropertyChanged(nameof(Address)); this.RaisePropertyChanged(nameof(AmountText)); this.RaisePropertyChanged(nameof(Privacy)); this.RaisePropertyChanged(nameof(Status)); }
}

internal sealed class MobileActionCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
