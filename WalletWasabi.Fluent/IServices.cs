using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Models;
using WalletWasabi.Services;
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
	void RenameWallet(Wallet wallet, string newWalletName);
	string GetWalletsDir();
	string GetNextWalletName(string prefix);
	string GetWalletFilePath(string walletName);
	(ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName);
	Task StartWalletAsync(Wallet wallet);
	void AddWallet(KeyManager keyManager);
	Task<KeyManager> ImportWalletAsync(string walletName, string filePath);
	IObservable<Wallet> SubscribeToWalletAdded();

	string GetTorLogFilePath();
	TorMode GetUseTor();

	decimal GetUsdExchangeRate();

	T? GetHostedService<T>() where T : class, Microsoft.Extensions.Hosting.IHostedService;

	Task SendTransactionAsync(SmartTransaction transaction);

	HttpClient CreateHttpClient(string name);

	bool IsForcefulTerminationRequested();
}
