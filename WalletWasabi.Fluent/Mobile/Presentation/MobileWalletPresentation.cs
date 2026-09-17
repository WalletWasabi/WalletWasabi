using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Mobile.Presentation;

/// <summary>
/// View-owned adapter over the live wallet projection. Commands are passed through unchanged.
/// It owns only event subscriptions and row adapters, not the wallet or its commands.
/// </summary>
public sealed class MobileWalletPresentation : ReactiveObject, IMobileWalletPresentation, IDisposable
{
	private readonly MobileWalletViewModel _source;
	private readonly Dictionary<MobileCoinItem, LiveCoin> _coins = new(ReferenceEqualityComparer.Instance);
	private readonly ObservableCollection<IMobileCoinPresentation> _coinRows = new();
	private LiveCoinJoin? _coinJoin;
	private bool _disposed;

	public MobileWalletPresentation(MobileWalletViewModel source)
	{
		_source = source ?? throw new ArgumentNullException(nameof(source));
		_source.PropertyChanged += SourceChanged;
		_source.Wallet.PropertyChanged += WalletChanged;
		_source.Coins.CollectionChanged += CoinsChanged;
		try { RefreshCoinJoin(); RefreshCoins(); }
		catch { Dispose(); throw; }
	}

	public string WalletTitle => _source.Wallet.Title;
	public string Section => _source.Section;
	public string Query { get => _source.Query; set => _source.Query = value; }
	public string Filter => _source.Filter;
	public string Message => _source.Message;
	public string BalanceText => _source.BalanceText;
	public string FiatBalanceText => _source.FiatBalanceText;
	public IReadOnlyList<double> BalanceHistory => _source.BalanceHistory;
	public bool ShowHistoryChart => _source.ShowHistoryChart;
	public bool IsEmpty => _source.IsEmpty;
	public bool IsBusy => _source.IsBusy;
	public bool CanSend => _source.Wallet.IsSendButtonVisible;
	public int PrivacyPercent => _source.PrivacyPercent;
	public string PrivacyDescription => _source.PrivacyDescription;
	public int CoinCount => _source.CoinCount;
	public int PrivateCoinCount => _source.PrivateCoinCount;
	public int PendingCoinCount => _source.PendingCoinCount;
	public string SelectionText => _source.SelectionText;
	public string NetworkName => _source.NetworkName;
	public bool HasCoinjoin => _coinJoin is not null;
	public IMobileCoinJoinPresentation? CoinJoin => _coinJoin;
	public IEnumerable<MobileTransactionItem> Transactions => _source.Transactions;
	public IEnumerable<MobileTransactionItem> RecentTransactions => _source.RecentTransactions;
	public IEnumerable<IMobileCoinPresentation> Coins => _coinRows;
	public MobileTransactionItem? SelectedTransaction => _source.SelectedTransaction;
	public ICommand NavigateCommand => _source.NavigateCommand;
	public ICommand ToggleDiscreetCommand => _source.ToggleDiscreetCommand;
	public ICommand SwitchWalletCommand => _source.SwitchWalletCommand;
	public ICommand SelectFilterCommand => _source.SelectFilterCommand;
	public ICommand ExcludeSelectedCommand => _source.ExcludeSelectedCommand;
	public ICommand IncludeSelectedCommand => _source.IncludeSelectedCommand;
	public ICommand CopyTransactionIdCommand => _source.CopyTransactionIdCommand;
	public ICommand OpenExplorerCommand => _source.OpenExplorerCommand;
	public ICommand SendCommand => _source.Wallet.DefaultSendCommand;
	public ICommand ReceiveCommand => _source.Wallet.DefaultReceiveCommand;
	public ICommand WalletSettingsCommand => _source.Wallet.WalletSettingsCommand;
	public ICommand CoinJoinSettingsCommand => _source.Wallet.CoinJoinSettingsCommand;
	public ICommand CoordinatorSettingsCommand => _source.Wallet.NavigateToCoordinatorSettingsCommand;
	public ICommand CoinJoinPaymentsCommand => _source.Wallet.CoinJoinPaymentsCommand;
	public ICommand CoordinatorHelpCommand => _source.Wallet.CoordinatorHelpCommand;
	public ICommand AdvancedCoinControlCommand => _source.Wallet.WalletCoinsCommand;
	public ICommand WalletInfoCommand => _source.Wallet.WalletInfoCommand;
	public ICommand WalletStatsCommand => _source.Wallet.WalletStatsCommand;
	public ICommand? ApplicationSettingsCommand => _source.Wallet.UiContext.MainViewModel?.NavigateToMobileSettingsCommand;

	private void SourceChanged(object? sender, PropertyChangedEventArgs e) => this.RaisePropertyChanged(e.PropertyName);

	private void WalletChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_disposed) return;
		switch (e.PropertyName)
		{
			case nameof(WalletViewModel.Title): this.RaisePropertyChanged(nameof(WalletTitle)); break;
			case nameof(WalletViewModel.IsSendButtonVisible): this.RaisePropertyChanged(nameof(CanSend)); break;
			case nameof(WalletViewModel.DefaultSendCommand): this.RaisePropertyChanged(nameof(SendCommand)); break;
			case nameof(WalletViewModel.DefaultReceiveCommand): this.RaisePropertyChanged(nameof(ReceiveCommand)); break;
			case nameof(WalletViewModel.CoinJoinStateViewModel): RefreshCoinJoin(); break;
			case null:
			case "": RefreshCoinJoin(); this.RaisePropertyChanged(string.Empty); break;
		}
	}

	private void RefreshCoinJoin()
	{
		var source = _source.Wallet.CoinJoinStateViewModel;
		if (ReferenceEquals(_coinJoin?.Source, source)) return;
		_coinJoin?.Dispose();
		_coinJoin = source is null ? null : new LiveCoinJoin(source);
		this.RaisePropertyChanged(nameof(CoinJoin));
		this.RaisePropertyChanged(nameof(HasCoinjoin));
	}

	private void CoinsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshCoins();

	private void RefreshCoins()
	{
		if (_disposed) return;
		var desired = new List<IMobileCoinPresentation>(_source.Coins.Count);
		var present = new HashSet<MobileCoinItem>(ReferenceEqualityComparer.Instance);
		foreach (var source in _source.Coins)
		{
			present.Add(source);
			if (!_coins.TryGetValue(source, out var row)) _coins.Add(source, row = new LiveCoin(source));
			desired.Add(row);
		}
		MobileCollectionSync.Reconcile(_coinRows, desired);
		foreach (var key in _coins.Keys.Where(key => !present.Contains(key)).ToArray())
		{
			_coins[key].Dispose();
			_coins.Remove(key);
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_source.PropertyChanged -= SourceChanged;
		_source.Wallet.PropertyChanged -= WalletChanged;
		_source.Coins.CollectionChanged -= CoinsChanged;
		_coinJoin?.Dispose();
		foreach (var row in _coins.Values) row.Dispose();
		_coins.Clear();
		_coinRows.Clear();
	}

	private sealed class LiveCoin : ReactiveObject, IMobileCoinPresentation, IDisposable
	{
		private readonly MobileCoinItem _source;
		public LiveCoin(MobileCoinItem source) { _source = source; source.PropertyChanged += HandleSourcePropertyChanged; }
		public string Address => _source.Address;
		public string AmountText => _source.AmountText;
		public string Privacy => _source.Privacy;
		public string Status => _source.Status;
		public bool IsSelected { get => _source.IsSelected; set => _source.IsSelected = value; }
		private void HandleSourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => this.RaisePropertyChanged(e.PropertyName);
		public void Dispose() => _source.PropertyChanged -= HandleSourcePropertyChanged;
	}

	private sealed class LiveCoinJoin : ReactiveObject, IMobileCoinJoinPresentation, IDisposable
	{
		public LiveCoinJoin(CoinJoinStateViewModel source) { Source = source; source.PropertyChanged += HandleSourcePropertyChanged; }
		public CoinJoinStateViewModel Source { get; }
		public double ProgressValue => Source.ProgressValue;
		public string CurrentStatus => Source.CurrentStatus;
		public string LeftText => Source.LeftText;
		public string RightText => Source.RightText;
		public bool IsInCriticalPhase => Source.IsInCriticalPhase;
		public bool PlayVisible => Source.PlayVisible;
		public bool PauseVisible => Source.PauseVisible;
		public bool StopVisible => Source.StopVisible;
		public ICommand PlayCommand => Source.PlayCommand;
		public ICommand StopPauseCommand => Source.StopPauseCommand;
		private void HandleSourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => this.RaisePropertyChanged(e.PropertyName);
		public void Dispose() => Source.PropertyChanged -= HandleSourcePropertyChanged;
	}
}
