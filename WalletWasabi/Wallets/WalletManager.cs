using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets;

public class WalletManager
{
	/// <remarks>All access must be guarded by <see cref="Lock"/> object.</remarks>
	private volatile bool _disposedValue = false;

	public WalletManager(Network network, string workDir, WalletDirectories walletDirectories)
	{
		using (BenchmarkLogger.Measure())
		{
			Network = Guard.NotNull(nameof(network), network);
			WorkDir = Guard.NotNullOrEmptyOrWhitespace(nameof(workDir), workDir, true);
			Directory.CreateDirectory(WorkDir);
			WalletDirectories = Guard.NotNull(nameof(walletDirectories), walletDirectories);

			RefreshWalletList();
		}
	}

	/// <summary>
	/// Triggered if any of the Wallets processes a transaction. The sender of the event will be the Wallet.
	/// </summary>
	public event EventHandler<ProcessedResult>? WalletRelevantTransactionProcessed;

	/// <summary>
	/// Triggered if any of the Wallets changes its state. The sender of the event will be the Wallet.
	/// </summary>
	public event EventHandler<WalletState>? WalletStateChanged;

	/// <summary>
	/// Triggered if a wallet added to the Wallet collection. The sender of the event will be the WalletManager and the argument is the added Wallet.
	/// </summary>
	public event EventHandler<Wallet>? WalletAdded;

	private CancellationTokenSource CancelAllInitialization { get; } = new();

	/// <remarks>All access must be guarded by <see cref="Lock"/> object.</remarks>
	private HashSet<Wallet> Wallets { get; } = new();

	private object Lock { get; } = new();
	private AsyncLock StartStopWalletLock { get; } = new();

	private BitcoinStore BitcoinStore { get; set; }
	public WasabiSynchronizer? Synchronizer { get; private set; }
	private ServiceConfiguration ServiceConfiguration { get; set; }
	private bool IsInitialized { get; set; }

	private HybridFeeProvider FeeProvider { get; set; }
	public Network Network { get; }
	public WalletDirectories WalletDirectories { get; }
	private IBlockProvider BlockProvider { get; set; }
	public string WorkDir { get; }

	private void RefreshWalletList()
	{
		foreach (var fileInfo in WalletDirectories.EnumerateWalletFiles())
		{
			try
			{
				string walletName = Path.GetFileNameWithoutExtension(fileInfo.FullName);
				lock (Lock)
				{
					if (Wallets.Any(w => w.WalletName == walletName))
					{
						continue;
					}
				}
				AddWallet(walletName);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}

	public IEnumerable<Wallet> GetWallets(bool refreshWalletList = true)
	{
		if (refreshWalletList)
		{
			RefreshWalletList();
		}

		lock (Lock)
		{
			return Wallets.ToList();
		}
	}

	public bool HasWallet() => AnyWallet(_ => true);

	public bool AnyWallet(Func<Wallet, bool> predicate)
	{
		lock (Lock)
		{
			return Wallets.Any(predicate);
		}
	}

	public async Task<Wallet> StartWalletAsync(Wallet wallet)
	{
		Guard.NotNull(nameof(wallet), wallet);

		lock (Lock)
		{
			if (_disposedValue)
			{
				Logger.LogError("Object was already disposed.");
				throw new OperationCanceledException("Object was already disposed.");
			}

			if (CancelAllInitialization.IsCancellationRequested)
			{
				throw new OperationCanceledException($"Stopped loading {wallet}, because cancel was requested.");
			}

			// Throw an exception if the wallet was not added to the WalletManager.
			Wallets.Single(x => x == wallet);
		}

		wallet.SetWaitingForInitState();

		// Wait for the WalletManager to be initialized.
		while (!IsInitialized)
		{
			await Task.Delay(100, CancelAllInitialization.Token).ConfigureAwait(false);
		}

		if (wallet.State == WalletState.WaitingForInit)
		{
			wallet.RegisterServices(BitcoinStore, Synchronizer, ServiceConfiguration, FeeProvider, BlockProvider);
		}

		using (await StartStopWalletLock.LockAsync(CancelAllInitialization.Token).ConfigureAwait(false))
		{
			try
			{
				var cancel = CancelAllInitialization.Token;
				Logger.LogInfo($"Starting wallet '{wallet.WalletName}'...");
				await wallet.StartAsync(cancel).ConfigureAwait(false);
				Logger.LogInfo($"Wallet '{wallet.WalletName}' started.");
				cancel.ThrowIfCancellationRequested();

				return wallet;
			}
			catch
			{
				await wallet.StopAsync(CancellationToken.None).ConfigureAwait(false);
				throw;
			}
		}
	}

	public Task<Wallet> AddAndStartWalletAsync(KeyManager keyManager)
	{
		var wallet = AddWallet(keyManager);
		return StartWalletAsync(wallet);
	}

	public Wallet AddWallet(KeyManager keyManager)
	{
		Wallet wallet = new(WorkDir, Network, keyManager);
		AddWallet(wallet);
		return wallet;
	}

	private void AddWallet(string walletName)
	{
		(string walletFullPath, string walletBackupFullPath) = WalletDirectories.GetWalletFilePaths(walletName);
		Wallet wallet;
		try
		{
			wallet = new Wallet(WorkDir, Network, walletFullPath);
		}
		catch (Exception ex)
		{
			if (!File.Exists(walletBackupFullPath))
			{
				throw;
			}

			Logger.LogWarning($"Wallet got corrupted.\n" +
				$"Wallet Filepath: {walletFullPath}\n" +
				$"Trying to recover it from backup.\n" +
				$"Backup path: {walletBackupFullPath}\n" +
				$"Exception: {ex}");
			if (File.Exists(walletFullPath))
			{
				string corruptedWalletBackupPath = $"{walletBackupFullPath}_CorruptedBackup";
				if (File.Exists(corruptedWalletBackupPath))
				{
					File.Delete(corruptedWalletBackupPath);
					Logger.LogInfo($"Deleted previous corrupted wallet file backup from `{corruptedWalletBackupPath}`.");
				}
				File.Move(walletFullPath, corruptedWalletBackupPath);
				Logger.LogInfo($"Backed up corrupted wallet file to `{corruptedWalletBackupPath}`.");
			}
			File.Copy(walletBackupFullPath, walletFullPath);

			wallet = new Wallet(WorkDir, Network, walletFullPath);
		}

		AddWallet(wallet);
	}

	private void AddWallet(Wallet wallet)
	{
		lock (Lock)
		{
			if (Wallets.Any(w => w.WalletName == wallet.WalletName))
			{
				throw new InvalidOperationException($"Wallet with the same name was already added: {wallet.WalletName}.");
			}
			Wallets.Add(wallet);
		}

		if (!File.Exists(WalletDirectories.GetWalletFilePaths(wallet.WalletName).walletFilePath))
		{
			wallet.KeyManager.ToFile();
		}

		wallet.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
		wallet.StateChanged += Wallet_StateChanged;

		WalletAdded?.Invoke(this, wallet);
	}

	public bool WalletExists(HDFingerprint? fingerprint) => GetWallets().Any(x => fingerprint is { } && x.KeyManager.MasterFingerprint == fingerprint);

	private void TransactionProcessor_WalletRelevantTransactionProcessed(object? sender, ProcessedResult e)
	{
		WalletRelevantTransactionProcessed?.Invoke(sender, e);
	}

	private void Wallet_StateChanged(object? sender, WalletState e)
	{
		WalletStateChanged?.Invoke(sender, e);
	}

	public async Task RemoveAndStopAllAsync(CancellationToken cancel)
	{
		lock (Lock)
		{
			// Already disposed.
			if (_disposedValue)
			{
				return;
			}

			_disposedValue = true;
		}

		try
		{
			CancelAllInitialization?.Cancel();
			CancelAllInitialization?.Dispose();
		}
		catch (ObjectDisposedException)
		{
			Logger.LogWarning($"{nameof(CancelAllInitialization)} is disposed. This can occur due to an error while processing the wallet.");
		}

		using (await StartStopWalletLock.LockAsync(cancel).ConfigureAwait(false))
		{
			foreach (var wallet in GetWallets())
			{
				cancel.ThrowIfCancellationRequested();

				wallet.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				wallet.StateChanged -= Wallet_StateChanged;

				lock (Lock)
				{
					if (!Wallets.Remove(wallet))
					{
						throw new InvalidOperationException("Wallet service doesn't exist.");
					}
				}

				try
				{
					if (wallet.State >= WalletState.Initialized)
					{
						var keyManager = wallet.KeyManager;
						string backupWalletFilePath = WalletDirectories.GetWalletFilePaths(Path.GetFileName(keyManager.FilePath)!).walletBackupFilePath;
						keyManager.ToFile(backupWalletFilePath);
						Logger.LogInfo($"{nameof(wallet.KeyManager)} backup saved to `{backupWalletFilePath}`.");
						await wallet.StopAsync(cancel).ConfigureAwait(false);
						Logger.LogInfo($"'{wallet.WalletName}' wallet is stopped.");
					}
					wallet?.Dispose();
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}
			}
		}
	}

	public void ProcessCoinJoin(SmartTransaction tx)
	{
		lock (Lock)
		{
			foreach (var wallet in Wallets.Where(x => x.State == WalletState.Started && !x.TransactionProcessor.IsAware(tx.GetHash())))
			{
				wallet.TransactionProcessor.Process(tx);
			}
		}
	}

	public void Process(SmartTransaction transaction)
	{
		lock (Lock)
		{
			foreach (var wallet in Wallets.Where(x => x.State == WalletState.Started))
			{
				wallet.TransactionProcessor.Process(transaction);
			}
		}
	}

	public IEnumerable<SmartCoin> CoinsByOutPoint(OutPoint input)
	{
		lock (Lock)
		{
			var res = new List<SmartCoin>();
			foreach (var wallet in Wallets.Where(x => x.State == WalletState.Started))
			{
				if (wallet.Coins.TryGetByOutPoint(input, out var coin))
				{
					res.Add(coin);
				}
			}

			return res;
		}
	}

	public ISet<uint256> FilterUnknownCoinjoins(IEnumerable<uint256> cjs)
	{
		lock (Lock)
		{
			var unknowns = new HashSet<uint256>();
			foreach (var wallet in Wallets.Where(x => x.State == WalletState.Started))
			{
				// If a wallet service doesn't know about the tx, then we add it for processing.
				foreach (var tx in cjs.Where(x => !wallet.TransactionProcessor.IsAware(x)))
				{
					unknowns.Add(tx);
				}
			}
			return unknowns;
		}
	}

	public void RegisterServices(BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, ServiceConfiguration serviceConfiguration, HybridFeeProvider feeProvider, IBlockProvider blockProvider)
	{
		BitcoinStore = bitcoinStore;
		Synchronizer = synchronizer;
		ServiceConfiguration = serviceConfiguration;
		FeeProvider = feeProvider;
		BlockProvider = blockProvider;

		foreach (var wallet in GetWallets().Where(w => w.State == WalletState.WaitingForInit))
		{
			wallet.RegisterServices(BitcoinStore, Synchronizer, ServiceConfiguration, FeeProvider, BlockProvider);
		}

		IsInitialized = true;
	}

	public void SetMaxBestHeight(uint bestHeight)
	{
		foreach (var km in GetWallets(refreshWalletList: false).Select(x => x.KeyManager).Where(x => x.GetNetwork() == Network))
		{
			km.SetMaxBestHeight(new Height(bestHeight));
		}
	}

	/// <param name="refreshWalletList">Refreshes wallet list from files.</param>
	public Wallet GetWalletByName(string walletName, bool refreshWalletList = true)
	{
		if (refreshWalletList)
		{
			RefreshWalletList();
		}
		lock (Lock)
		{
			return Wallets.Single(x => x.KeyManager.WalletName == walletName);
		}
	}
}
