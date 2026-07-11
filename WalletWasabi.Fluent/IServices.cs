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
	string DataDir { get; }
	string PersistentConfigFilePath { get; }
	PersistentConfig PersistentConfig { get; }
	WalletManager WalletManager { get; }
	UiConfig UiConfig { get; }
	Config Config { get; }
	EventBus EventBus { get; }
	Client.Scheme Scheme { get; }

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

	string GetTorLogFilePath();
	TorMode GetUseTor();

	decimal GetUsdExchangeRate();

	bool GetHideOnClose();
	double? GetWindowWidth();
	double? GetWindowHeight();
	void SetWindowWidth(double? width);
	void SetWindowHeight(double? height);
	string? GetLastSelectedWallet();
	void SetLastSelectedWallet(string? walletName);
	bool GetPrivacyMode();
	bool GetAutocopy();
	bool GetAutoPaste();
	bool GetSendAmountConversionReversed();
	void SetSendAmountConversionReversed(bool value);
	int GetFeeTarget();
	void SetFeeTarget(int value);

	T? GetHostedService<T>() where T : class, Microsoft.Extensions.Hosting.IHostedService;

	Task SendTransactionAsync(SmartTransaction transaction);

	HttpClient CreateHttpClient(string name);

	bool IsForcefulTerminationRequested();
}
