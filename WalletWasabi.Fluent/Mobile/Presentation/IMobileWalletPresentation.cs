using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using WalletWasabi.Fluent.Mobile.ViewModels;

namespace WalletWasabi.Fluent.Mobile.Presentation;

/// <summary>
/// Native wallet rendering contract. It contains presentation state and commands, never
/// a key manager, wallet service, broadcaster, or authorization result.
/// </summary>
public interface IMobileWalletPresentation : INotifyPropertyChanged
{
	string WalletTitle { get; }
	string Section { get; }
	string Query { get; set; }
	string Filter { get; }
	string Message { get; }
	string BalanceText { get; }
	string FiatBalanceText { get; }
	IReadOnlyList<double> BalanceHistory { get; }
	bool ShowHistoryChart { get; }
	bool IsEmpty { get; }
	bool IsBusy { get; }
	bool CanSend { get; }
	int PrivacyPercent { get; }
	string PrivacyDescription { get; }
	int CoinCount { get; }
	int PrivateCoinCount { get; }
	int PendingCoinCount { get; }
	string SelectionText { get; }
	string NetworkName { get; }
	bool HasCoinjoin { get; }
	IMobileCoinJoinPresentation? CoinJoin { get; }
	IEnumerable<MobileTransactionItem> Transactions { get; }
	IEnumerable<MobileTransactionItem> RecentTransactions { get; }
	IEnumerable<IMobileCoinPresentation> Coins { get; }
	MobileTransactionItem? SelectedTransaction { get; }
	ICommand NavigateCommand { get; }
	ICommand ToggleDiscreetCommand { get; }
	ICommand SwitchWalletCommand { get; }
	ICommand SelectFilterCommand { get; }
	ICommand ExcludeSelectedCommand { get; }
	ICommand IncludeSelectedCommand { get; }
	ICommand CopyTransactionIdCommand { get; }
	ICommand OpenExplorerCommand { get; }
	ICommand SendCommand { get; }
	ICommand ReceiveCommand { get; }
	ICommand WalletSettingsCommand { get; }
	ICommand CoinJoinSettingsCommand { get; }
	ICommand CoordinatorSettingsCommand { get; }
	ICommand CoinJoinPaymentsCommand { get; }
	ICommand CoordinatorHelpCommand { get; }
	ICommand AdvancedCoinControlCommand { get; }
	ICommand WalletInfoCommand { get; }
	ICommand WalletStatsCommand { get; }
	ICommand? ApplicationSettingsCommand { get; }
}

public interface IMobileCoinPresentation : INotifyPropertyChanged
{
	string Address { get; }
	string AmountText { get; }
	string Privacy { get; }
	string Status { get; }
	bool IsSelected { get; set; }
}

public interface IMobileCoinJoinPresentation : INotifyPropertyChanged
{
	double ProgressValue { get; }
	string CurrentStatus { get; }
	string LeftText { get; }
	string RightText { get; }
	bool IsInCriticalPhase { get; }
	bool PlayVisible { get; }
	bool PauseVisible { get; }
	bool StopVisible { get; }
	ICommand PlayCommand { get; }
	ICommand StopPauseCommand { get; }
}
