using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Fluent;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Wallets;
using Scheme = WalletWasabi.Client.Scheme;
using WasabiWallet = WalletWasabi.Wallets.Wallet;

namespace WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

/// <summary>
/// Minimal <see cref="IServices"/> implementation for constructing Fluent view models in
/// unit tests. Read-style members return benign defaults; anything that would mutate real
/// wallet state throws so a test cannot silently depend on it.
/// </summary>
public class FakeServices : IServices
{
	private readonly List<IHostedService> _hostedServices = new();

	public FakeServices(string dataDir, string[]? cliArgs = null)
	{
		DataDir = dataDir;
		PersistentConfig = PersistentConfigManager.DefaultMainNetConfig;
		Config = new Config(PersistentConfig, cliArgs ?? []);
		UiConfig = new UiConfig(Path.Combine(dataDir, "UiConfig.json"));
		EventBus = new EventBus();
		WalletManager = new WalletManager(
			Network.Main,
			new WalletDirectories(Network.Main, Path.Combine(dataDir, "Wallets")),
			_ => throw new NotSupportedException("FakeServices cannot create wallets."));
	}

	public string DataDir { get; }
	public string PersistentConfigFilePath => Path.Combine(DataDir, "Config.json");
	public PersistentConfig PersistentConfig { get; }
	public WalletManager WalletManager { get; }
	public UiConfig UiConfig { get; }
	public Config Config { get; }
	public EventBus EventBus { get; }
	public Scheme Scheme => null!;

	public void AddHostedService(IHostedService service) => _hostedServices.Add(service);

	public uint GetTipHeight() => 0;
	public uint GetServerTipHeight() => 0;
	public int GetHashesLeft() => 0;
	public SmartHeader? GetTip() => null;
	public uint GetBlockHeadersTipHeight() => 0;
	public int GetPeerCount() => 0;
	public uint? GetMinimumBlockHeight() => null;

	public IEnumerable<LabelsArray> GetTransactionLabels() => [];

	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		tx = null;
		return false;
	}

	public Network GetNetwork() => Network.Main;
	public IEnumerable<WasabiWallet> GetWallets() => [];
	public bool HasWallet() => false;
	public WasabiWallet GetWalletByName(string walletName) => throw new NotSupportedException();
	public void RenameWallet(WasabiWallet wallet, string newWalletName) => throw new NotSupportedException();
	public string GetWalletsDir() => Path.Combine(DataDir, "Wallets");
	public string GetNextWalletName(string prefix) => $"{prefix} 1";
	public string GetWalletFilePath(string walletName) => Path.Combine(GetWalletsDir(), $"{walletName}.json");
	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName) => null;
	public Task StartWalletAsync(WasabiWallet wallet) => throw new NotSupportedException();
	public void AddWallet(KeyManager keyManager) => throw new NotSupportedException();

	public string GetTorLogFilePath() => Path.Combine(DataDir, "TorLogs.txt");
	public TorMode GetUseTor() => TorMode.Disabled;

	public decimal GetUsdExchangeRate() => 0m;

	public bool GetHideOnClose() => false;
	public double? GetWindowWidth() => null;
	public double? GetWindowHeight() => null;

	public void SetWindowWidth(double? width)
	{
	}

	public void SetWindowHeight(double? height)
	{
	}

	public string? GetLastSelectedWallet() => null;

	public void SetLastSelectedWallet(string? walletName)
	{
	}

	public bool GetPrivacyMode() => false;
	public bool GetAutocopy() => false;
	public bool GetAutoPaste() => false;
	public bool GetSendAmountConversionReversed() => false;

	public void SetSendAmountConversionReversed(bool value)
	{
	}

	public int GetFeeTarget() => 2;

	public void SetFeeTarget(int value)
	{
	}

	public T? GetHostedService<T>() where T : class, IHostedService => _hostedServices.OfType<T>().FirstOrDefault();

	public Task SendTransactionAsync(SmartTransaction transaction) => throw new NotSupportedException();

	public HttpClient CreateHttpClient(string name) => new();

	public bool IsForcefulTerminationRequested() => false;
}
