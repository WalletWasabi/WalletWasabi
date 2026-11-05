using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public interface IServices
{
	string GetDataDir();
	string GetPersistentConfigFilePath();
	Client.Scheme GetScheme();

	// Event subscription
	IObservable<T> SubscribeToEvent<T>() where T : notnull;
	void PublishEvent<T>(T evt) where T : notnull;
	Task WaitForEventAsync<T>(Func<bool> condition) where T : notnull;

	// Config
	string GetBitcoinRpcUri();
	bool GetEnableGpu();
	ApplicationSettingsData GetApplicationSettings();
	ApplicationSettingsData GetDefaultApplicationSettings(Network network);
	bool SaveApplicationSettings(ApplicationSettingsData settings);

	uint GetTipHeight();
	uint GetServerTipHeight();
	int GetHashesLeft();
	SmartHeader? GetTip();
	uint GetBlockHeadersTipHeight();
	int GetPeerCount();

	uint? GetMinimumBlockHeight();

	IEnumerable<LabelsArray> GetTransactionLabels();
	bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx);

	Network GetNetwork();

	// Wallet management
	IEnumerable<WalletData> GetWallets();
	bool HasWallet();
	WalletId GetWalletIdByName(string walletName);
	void RenameWallet(string walletName, string newWalletName);
	string GetWalletsDir();
	string GetNextWalletName(string prefix);
	string GetWalletFilePath(string walletName);
	(ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName);
	Task StartWalletAsync(string walletName);
	void AddWallet(KeyManager keyManager);
	Task<KeyManager> ImportWalletAsync(string walletName, string filePath);
	IObservable<WalletId> SubscribeToWalletAdded();

	// Wallet state
	IObservable<bool> SubscribeToWalletLoaded(WalletId walletId);
	bool IsWalletLoaded(WalletId walletId);
	int GetAnonScoreTarget(WalletId walletId);

	// Wallet authentication
	bool HasPassword(WalletId walletId);
	string? GetPassword(WalletId walletId);
	Task<bool> TryLoginAsync(WalletId walletId, string password);
	Task<(bool IsValid, string? CompatibilityPassword)> TryLoginWithCompatibilityAsync(WalletId walletId, string password);
	Task<bool> IsPasswordValidAsync(WalletId walletId, string password);
	void Logout(WalletId walletId);
	Task<bool> VerifyRecoveryWordsAsync(WalletId walletId, Mnemonic mnemonic);

	// Wallet balance and coins
	Money GetWalletBalance(WalletId walletId);
	IEnumerable<SmartCoin> GetWalletCoins(WalletId walletId);

	// Wallet labels
	IEnumerable<(string Label, int Score)> GetLabelsWithRanking(WalletId walletId, Intent intent);

	// Wallet addresses
	IEnumerable<HdPubKey> GetUnusedKeys(WalletId walletId);
	HdPubKey GetNextReceiveAddress(WalletId walletId, LabelsArray labels, ScriptPubKeyType scriptType);
	void SetKeyState(WalletId walletId, KeyState state, HdPubKey key);
	void SaveKeyManager(WalletId walletId);

	// Wallet transactions
	Task<IEnumerable<TransactionSummary>> BuildHistorySummaryAsync(WalletId walletId, CancellationToken cancellationToken);
	bool TryGetWalletTransaction(WalletId walletId, uint256 txId, [NotNullWhen(true)] out SmartTransaction? tx);
	Task<TimeSpan?> EstimateConfirmationTimeAsync(WalletId walletId, uint256 txId, CancellationToken cancellationToken);
	Task<BuildTransactionResult> SpeedUpTransactionAsync(WalletId walletId, uint256 txId, CancellationToken cancellationToken);
	BuildTransactionResult CancelTransaction(WalletId walletId, uint256 txId);
	void UpdateTransactionLabels(WalletId walletId, Dictionary<HdPubKey, LabelsArray> hdPubKeysWithNewLabels);
	IEnumerable<Output> GetTransactionOutputs(WalletId walletId, uint256 txId);
	bool HasFeeRateEstimations(WalletId walletId);
	bool IsTransactionCancellable(WalletId walletId, uint256 txId);
	bool IsTransactionSpeedupable(WalletId walletId, uint256 txId);
	CoinjoinCosts? GetCoinjoinCosts(WalletId walletId, uint256 txId);

	string GetTorLogFilePath();
	TorMode GetUseTor();

	decimal GetUsdExchangeRate();

	// CoinJoin
	bool IsCoinJoinEnabled();
	CoinJoinClientState GetHighestCoinJoinClientState();
	ImmutableList<SmartCoin> GetCoinsInCriticalPhase(WalletId walletId);
	Task RestartAbortedCoinjoinsAsync();
	Task SignalToStopCoinjoinsAsync();
	void WalletEnteredSendWorkflow(WalletId walletId);
	void WalletLeftSendWorkflow(WalletId walletId);
	Task WalletEnteredSendingAsync(WalletId walletId);
	IObservable<StatusChangedEventArgs> SubscribeToCoinJoinStatusChanged();
	void RequestCoinJoinStart(WalletId walletId, WalletId outputWalletId, bool stopWhenAllMixed, bool overridePlebStop);
	void RequestCoinJoinStop(WalletId walletId);

	Task SendTransactionAsync(SmartTransaction transaction);

	HttpClient CreateHttpClient(string name);

	bool IsForcefulTerminationRequested();
}
