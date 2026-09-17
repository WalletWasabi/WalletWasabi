using System.Collections.ObjectModel;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Mobile.Presentation;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>
/// Deterministic, non-payment test data for the real production rendering contract.
/// This type is compiled only into the test assembly and has no wallet or network services.
/// </summary>
internal sealed class MobileWalletFixture : ReactiveObject, IMobileWalletPresentation
{
	private string _section = "home";
	private string _filter = "All";
	private string _query = "";
	private string _message = "";
	private bool _hidden;
	private MobileTransactionItem? _selected;
	private readonly List<MobileTransactionItem> _all;
	private readonly List<CoinFixture> _coins;
	private readonly ObservableCollection<MobileTransactionItem> _visible = new();
	private readonly TestCommand _include;
	private readonly TestCommand _exclude;
	private readonly CoinJoinFixture _coinjoin = new();
	public List<string> Invocations { get; } = new();

	public MobileWalletFixture(int transactionCount = 4)
	{
		_all = Enumerable.Range(0, transactionCount).Select(index => new MobileTransactionItem(CreateTransaction(index), false, 68_432m, OpenTransaction)).ToList();
		_coins = Enumerable.Range(0, 12).Select(index => new CoinFixture(index, () => _hidden, SelectionChanged)).ToList();
		NavigateCommand = new TestCommand(value => Navigate((string?)value ?? "home"));
		SelectFilterCommand = new TestCommand(value => { _filter = (string?)value ?? "All"; this.RaisePropertyChanged(nameof(Filter)); RefreshRows(); });
		ToggleDiscreetCommand = new TestCommand(_ =>
		{
			_hidden = !_hidden;
			foreach (var row in _all) row.Update(row.Model, _hidden, 68_432m);
			foreach (var row in _coins) row.Refresh();
			this.RaisePropertyChanged(nameof(BalanceText)); this.RaisePropertyChanged(nameof(FiatBalanceText)); this.RaisePropertyChanged(nameof(ShowHistoryChart));
		});
		_include = new TestCommand(_ => Exclude(false), _ => _coins.Any(x => x.IsSelected));
		_exclude = new TestCommand(_ => Exclude(true), _ => _coins.Any(x => x.IsSelected));
		SwitchWalletCommand = Record("switch-wallet"); SendCommand = Record("send"); ReceiveCommand = Record("receive");
		WalletSettingsCommand = Record("wallet-settings"); CoinJoinSettingsCommand = Record("coinjoin-settings");
		CoordinatorSettingsCommand = Record("coordinator-settings"); CoinJoinPaymentsCommand = Record("coinjoin-payments");
		CoordinatorHelpCommand = Record("coordinator-help"); AdvancedCoinControlCommand = Record("advanced-coins");
		WalletInfoCommand = Record("wallet-info"); WalletStatsCommand = Record("wallet-statistics");
		ApplicationSettingsCommand = Record("application-settings"); CopyTransactionIdCommand = Record("copy-id"); OpenExplorerCommand = Record("explorer");
		_selected = _all.FirstOrDefault();
		RefreshRows();
	}
	public string WalletTitle => "Personal wallet";
	public string Section => _section;
	public string Query { get => _query; set { this.RaiseAndSetIfChanged(ref _query, value); RefreshRows(); } }
	public string Filter => _filter;
	public string Message => _message;
	public string BalanceText => _hidden ? "•••••• BTC" : "0.2456 BTC";
	public string FiatBalanceText => _hidden ? "•••••• USD" : "≈ $16,806.90 USD";
	public IReadOnlyList<double> BalanceHistory { get; } = new[] { 0.18, 0.181, 0.185, 0.182, 0.21, 0.225, 0.219, 0.2456 };
	public bool ShowHistoryChart => !_hidden;
	public bool IsEmpty => _visible.Count == 0;
	public bool IsBusy => false;
	public bool CanSend => true;
	public int PrivacyPercent => 92;
	public string PrivacyDescription => "Share of balance meeting the wallet's anonymity target. Not a guarantee of anonymity.";
	public int CoinCount => _coins.Count;
	public int PrivateCoinCount => 9;
	public int PendingCoinCount => 2;
	public string SelectionText => $"{_coins.Count(x => x.IsSelected)} selected · {_coins.Count(x => x.Excluded)} excluded from CoinJoin";
	public string NetworkName => "RegTest · synthetic fixture";
	public bool HasCoinjoin => true;
	public IMobileCoinJoinPresentation CoinJoin => _coinjoin;
	public IEnumerable<MobileTransactionItem> Transactions => _visible;
	public IEnumerable<MobileTransactionItem> RecentTransactions => _all.Take(4);
	public IEnumerable<IMobileCoinPresentation> Coins => _coins;
	public MobileTransactionItem? SelectedTransaction => _selected;
	public ICommand NavigateCommand { get; }
	public ICommand ToggleDiscreetCommand { get; }
	public ICommand SwitchWalletCommand { get; }
	public ICommand SelectFilterCommand { get; }
	public ICommand ExcludeSelectedCommand => _exclude;
	public ICommand IncludeSelectedCommand => _include;
	public ICommand CopyTransactionIdCommand { get; }
	public ICommand OpenExplorerCommand { get; }
	public ICommand SendCommand { get; }
	public ICommand ReceiveCommand { get; }
	public ICommand WalletSettingsCommand { get; }
	public ICommand CoinJoinSettingsCommand { get; }
	public ICommand CoordinatorSettingsCommand { get; }
	public ICommand CoinJoinPaymentsCommand { get; }
	public ICommand CoordinatorHelpCommand { get; }
	public ICommand AdvancedCoinControlCommand { get; }
	public ICommand WalletInfoCommand { get; }
	public ICommand WalletStatsCommand { get; }
	public ICommand? ApplicationSettingsCommand { get; }
	public void Navigate(string section)
	{
		if (section == "transaction" && _selected is null) _selected = _all.FirstOrDefault();
		this.RaisePropertyChanged(nameof(SelectedTransaction));
		this.RaiseAndSetIfChanged(ref _section, section, nameof(Section));
	}
	public void SetCriticalPhase(bool critical) => _coinjoin.SetCriticalPhase(critical);
	private TestCommand Record(string action) => new(_ => Invocations.Add(action));
	private void RefreshRows()
	{
		var desired = _all.Where(row => MobileHistoryProjection.MatchesFilter(row.Model.Type, _filter))
			.Where(row => string.IsNullOrWhiteSpace(_query) || row.Title.Contains(_query, StringComparison.OrdinalIgnoreCase) || row.Labels.Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
		MobileCollectionSync.Reconcile(_visible, desired);
		this.RaisePropertyChanged(nameof(IsEmpty));
	}
	private void OpenTransaction(MobileTransactionItem row) { _selected = row; this.RaisePropertyChanged(nameof(SelectedTransaction)); Navigate("transaction"); }
	private void SelectionChanged() { this.RaisePropertyChanged(nameof(SelectionText)); _include?.Refresh(); _exclude?.Refresh(); }
	private void Exclude(bool exclude)
	{
		foreach (var row in _coins.Where(x => x.IsSelected)) { row.Excluded = exclude; row.Refresh(); }
		_message = exclude ? "Selected coins excluded from CoinJoin. They remain spendable." : "Selected coins eligible for CoinJoin.";
		this.RaisePropertyChanged(nameof(Message)); SelectionChanged();
	}
	private static TransactionModel CreateTransaction(int index) => new()
	{
		Id = new uint256((ulong)index + 1), OrderIndex = index,
		Type = index % 3 == 0 ? TransactionType.IncomingTransaction : index % 3 == 1 ? TransactionType.OutgoingTransaction : TransactionType.Coinjoin,
		Amount = Money.Satoshis(index % 3 == 0 ? 1_200_000 : index % 3 == 1 ? -500_000 : -210),
		Fee = Money.Satoshis(210), FeeRate = new FeeRate(5m), BlockHeight = 892_421,
		Date = new DateTimeOffset(2025, 4, 24, 14, 21, 0, TimeSpan.Zero).AddHours(-index * 12),
		DateString = "Apr 24, 2025", DateToolTipString = "Synthetic fixture",
		Labels = new LabelsArray(new[] { index % 2 == 0 ? "Savings" : "Everyday" }), Confirmations = 6,
		ConfirmedTooltip = "6 confirmations", Status = TransactionStatus.Confirmed,
		HexFunction = () => string.Empty, ForeignInputsFunction = () => Array.Empty<OutPoint>(),
		ForeignOutputsFunction = () => Array.Empty<IndexedTxOut>(), WalletInputs = Array.Empty<SmartCoin>(), WalletOutputs = Array.Empty<SmartCoin>()
	};
	private sealed class CoinFixture(int index, Func<bool> hidden, Action changed) : ReactiveObject, IMobileCoinPresentation
	{
		private bool _selected;
		public bool Excluded { get; set; }
		public string Address => hidden() ? "Hidden in discreet mode" : $"Test output {index + 1} · non-payment fixture";
		public string AmountText => hidden() ? "•••••• BTC" : $"{(index < 8 ? 0.02510577m : index == 8 ? 0.02510584m : index < 11 ? 0.00655m : 0.006548m):0.00000000} BTC";
		public string Privacy => index < 9 ? "Private" : "Non-private";
		public string Status => Excluded ? "Excluded from CoinJoin" : index is 9 or 10 ? "CoinJoin in progress" : "Confirmed";
		public bool IsSelected { get => _selected; set { if (_selected == value) return; this.RaiseAndSetIfChanged(ref _selected, value); changed(); } }
		public void Refresh() { this.RaisePropertyChanged(nameof(Address)); this.RaisePropertyChanged(nameof(AmountText)); this.RaisePropertyChanged(nameof(Status)); }
	}
	private sealed class CoinJoinFixture : ReactiveObject, IMobileCoinJoinPresentation
	{
		private bool _critical;
		private readonly TestCommand _stop;
		public CoinJoinFixture() { PlayCommand = new TestCommand(_ => { }); _stop = new TestCommand(_ => { }, _ => !_critical); }
		public double ProgressValue => 62;
		public string CurrentStatus => _critical ? "Signing transaction" : "Awaiting other participants";
		public string LeftText => "Round in progress";
		public string RightText => "00:42";
		public bool IsInCriticalPhase => _critical;
		public bool PlayVisible => false;
		public bool PauseVisible => true;
		public bool StopVisible => false;
		public ICommand PlayCommand { get; }
		public ICommand StopPauseCommand => _stop;
		public void SetCriticalPhase(bool critical) { this.RaiseAndSetIfChanged(ref _critical, critical, nameof(IsInCriticalPhase)); this.RaisePropertyChanged(nameof(CurrentStatus)); _stop.Refresh(); }
	}
	internal sealed class TestCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
	{
		public event EventHandler? CanExecuteChanged;
		public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
		public void Execute(object? parameter) { if (CanExecute(parameter)) execute(parameter); }
		public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}
