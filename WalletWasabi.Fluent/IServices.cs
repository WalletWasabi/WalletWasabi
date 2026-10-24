using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
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
	IEnumerable<Wallet> GetWallets();
	bool HasWallet();
	Wallet GetWalletByName(string walletName);
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
