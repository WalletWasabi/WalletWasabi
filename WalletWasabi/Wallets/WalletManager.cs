using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Wallets;

public class WalletManager : IWalletProvider
{
	/// <remarks>All access must be guarded by <see cref="_lock"/> object.</remarks>
	private volatile bool _disposedValue = false;

	public WalletManager(
		Network network,
		string workDir,
		WalletDirectories walletDirectories,
		WalletFactory walletFactory)
	{
		Network = network;
		_workDir = Guard.NotNullOrEmptyOrWhitespace(nameof(workDir), workDir, true);
		Directory.CreateDirectory(_workDir);
		WalletDirectories = walletDirectories;
		_walletFactory = walletFactory;
		_cancelAllTasksToken = _cancelAllTasks.Token;

		LoadWalletListFromFileSystem();
	}

	/// <summary>
	/// Triggered if any of the _wallets changes its state. The sender of the event will be the Wallet.
	/// </summary>
	public event EventHandler<WalletState>? WalletStateChanged;

	/// <summary>
	/// Triggered if a wallet added to the Wallet collection. The sender of the event will be the WalletManager and the argument is the added Wallet.
	/// </summary>
	public event EventHandler<Wallet>? WalletAdded;

	/// <summary>Cancels initialization of wallets.</summary>
	private readonly CancellationTokenSource _cancelAllTasks = new();

	/// <summary>Token from <see cref="_cancelAllTasks"/>.</summary>
	/// <remarks>Accessing the token of <see cref="_cancelAllTasks"/> can lead to <see cref="ObjectDisposedException"/>. So we copy the token and no exception can be thrown.</remarks>
	private readonly CancellationToken _cancelAllTasksToken;

	/// <remarks>All access must be guarded by <see cref="_lock"/> object.</remarks>
	private readonly HashSet<Wallet> _wallets = new();

	private readonly object _lock = new();
	private readonly AsyncLock _startStopWalletLock = new();

	private bool IsInitialized { get; set; }

	private readonly WalletFactory _walletFactory;
	public Network Network { get; }
	public WalletDirectories WalletDirectories { get; }
	private readonly string _workDir;

	private void LoadWalletListFromFileSystem()
	{
		var walletFileNames = WalletDirectories.EnumerateWalletFiles().Select(fi => Path.GetFileNameWithoutExtension(fi.FullName));

		string[]? walletNamesToLoad = null;
		lock (_lock)
		{
			walletNamesToLoad = walletFileNames.Where(walletFileName => !_wallets.Any(wallet => wallet.WalletName == walletFileName)).ToArray();
		}

		if (walletNamesToLoad.Length == 0)
		{
			return;
		}

		List<Task<Wallet>> walletLoadTasks = walletNamesToLoad.Select(walletName => Task.Run(() => LoadWalletByNameFromDisk(walletName), _cancelAllTasksToken)).ToList();

		while (walletLoadTasks.Count > 0)
		{
			var tasksArray = walletLoadTasks.ToArray();
			var finishedTaskIndex = Task.WaitAny(tasksArray, _cancelAllTasksToken);
			var finishedTask = tasksArray[finishedTaskIndex];
			walletLoadTasks.Remove(finishedTask);
			try
			{
				var wallet = finishedTask.Result;
				AddWallet(wallet);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}

	public void RenameWallet(Wallet wallet, string newWalletName)
	{
		if (newWalletName == wallet.WalletName)
		{
			return;
		}

		if (ValidateWalletName(newWalletName) is { } error)
		{
			Logger.LogWarning($"Invalid name '{newWalletName}' when attempting to rename '{error.Message}'");
			throw new InvalidOperationException($"Invalid name {newWalletName} - {error.Message}");
		}

		var currentWalletFilePath = WalletDirectories.GetWalletFilePaths(wallet.WalletName);
		var newWalletFilePath = WalletDirectories.GetWalletFilePaths(newWalletName);

		Logger.LogInfo($"Renaming file {currentWalletFilePath} to {newWalletFilePath}");
		File.Move(currentWalletFilePath, newWalletFilePath);

		wallet.KeyManager.SetFilePath(newWalletFilePath);
	}

	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName)
	{
		string walletFilePath = Path.Combine(WalletDirectories.WalletsDir, $"{walletName}.json");

		if (string.IsNullOrEmpty(walletName))
		{
			return (ErrorSeverity.Error, "The name cannot be empty");
		}

		if (walletName.IsTrimmable())
		{
			return (ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
		}

		if (File.Exists(walletFilePath))
		{
			return (ErrorSeverity.Error, $"A wallet named {walletName} already exists. Please try a different name.");
		}

		if (!WalletGenerator.ValidateWalletName(walletName))
		{
			return (ErrorSeverity.Error, "Selected wallet name is not valid. Please try a different name.");
		}

		return null;
	}

	public Task<IEnumerable<IWallet>> GetWalletsAsync() => Task.FromResult<IEnumerable<IWallet>>(GetWallets());

	public IEnumerable<Wallet> GetWallets()
	{
		lock (_lock)
		{
			return _wallets.ToList();
		}
	}

	public bool HasWallet()
	{
		lock (_lock)
		{
			return _wallets.Count > 0;
		}
	}

	public async Task<Wallet> StartWalletAsync(Wallet wallet)
	{
		lock (_lock)
		{
			if (_disposedValue)
			{
				Logger.LogError("Object was already disposed.");
				throw new OperationCanceledException("Object was already disposed.");
			}

			if (_cancelAllTasks.IsCancellationRequested)
			{
				throw new OperationCanceledException($"Stopped loading {wallet}, because cancel was requested.");
			}

			// Throw an exception if the wallet was not added to the WalletManager.
			_wallets.Single(x => x == wallet);
		}

		wallet.SetWaitingForInitState();

		// Wait for the WalletManager to be initialized.
		while (!IsInitialized)
		{
			await Task.Delay(100, _cancelAllTasks.Token).ConfigureAwait(false);
		}

		if (wallet.State == WalletState.WaitingForInit)
		{
			wallet.Initialize();
		}

		using (await _startStopWalletLock.LockAsync(_cancelAllTasks.Token).ConfigureAwait(false))
		{
			try
			{
				Logger.LogInfo($"Starting wallet '{wallet.WalletName}'...");
				await wallet.StartAsync(_cancelAllTasksToken).ConfigureAwait(false);
				Logger.LogInfo($"Wallet '{wallet.WalletName}' started.");
				_cancelAllTasksToken.ThrowIfCancellationRequested();
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
		Wallet wallet = _walletFactory.Create(keyManager);
		AddWallet(wallet);
		return wallet;
	}

	private Wallet LoadWalletByNameFromDisk(string walletName)
	{
		string walletFullPath = WalletDirectories.GetWalletFilePaths(walletName);
		try
		{
			return _walletFactory.Create(KeyManager.FromFile(walletFullPath));
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Wallet got corrupted.\n" +
				$"Wallet file path: {walletFullPath}\n" +
				$"Exception: {ex}");

			throw;
		}
	}

	private void AddWallet(Wallet wallet)
	{
		lock (_lock)
		{
			if (_wallets.Any(w => w.WalletId == wallet.WalletId))
			{
				throw new InvalidOperationException($"Wallet with the same name was already added: {wallet.WalletName}.");
			}
			_wallets.Add(wallet);
		}

		if (!File.Exists(WalletDirectories.GetWalletFilePaths(wallet.WalletName)))
		{
			wallet.KeyManager.ToFile();
		}

		wallet.StateChanged += Wallet_StateChanged;

		WalletAdded?.Invoke(this, wallet);
	}

	public bool WalletExists(HDFingerprint? fingerprint) => GetWallets().Any(x => fingerprint is { } && x.KeyManager.MasterFingerprint == fingerprint);

	private void Wallet_StateChanged(object? sender, WalletState e)
	{
		WalletStateChanged?.Invoke(sender, e);
	}

	public async Task RemoveAndStopAllAsync(CancellationToken cancel)
	{
		lock (_lock)
		{
			// Already disposed.
			if (_disposedValue)
			{
				return;
			}

			_disposedValue = true;
		}

		_cancelAllTasks.Cancel();

		using (await _startStopWalletLock.LockAsync(cancel).ConfigureAwait(false))
		{
			foreach (var wallet in GetWallets())
			{
				cancel.ThrowIfCancellationRequested();

				wallet.StateChanged -= Wallet_StateChanged;

				lock (_lock)
				{
					if (!_wallets.Remove(wallet))
					{
						throw new InvalidOperationException("Wallet service doesn't exist.");
					}
				}

				try
				{
					if (wallet.State >= WalletState.Initialized)
					{
						await wallet.StopAsync(cancel).ConfigureAwait(false);
						Logger.LogInfo($"'{wallet.WalletName}' wallet is stopped.");
					}

					wallet.Dispose();
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}
			}
		}

		_cancelAllTasks.Dispose();
	}

	public void ProcessCoinJoin(SmartTransaction tx)
	{
		lock (_lock)
		{
			foreach (var wallet in _wallets.Where(x => x.State == WalletState.Started && !x.TransactionProcessor.IsAware(tx.GetHash())))
			{
				wallet.TransactionProcessor.Process(tx);
			}
		}
	}

	public void Process(SmartTransaction transaction)
	{
		lock (_lock)
		{
			foreach (var wallet in _wallets.Where(x => x.State == WalletState.Started))
			{
				wallet.TransactionProcessor.Process(transaction);
			}
		}
	}

	public IEnumerable<SmartCoin> CoinsByOutPoint(OutPoint input)
	{
		lock (_lock)
		{
			var res = new List<SmartCoin>();
			foreach (var wallet in _wallets.Where(x => x.State == WalletState.Started))
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
		lock (_lock)
		{
			var unknowns = new HashSet<uint256>();
			foreach (var wallet in _wallets.Where(x => x.State == WalletState.Started))
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

	public void Initialize()
	{
		foreach (var wallet in GetWallets().Where(w => w.State == WalletState.WaitingForInit))
		{
			wallet.Initialize();
		}

		IsInitialized = true;
	}

	public void EnsureTurboSyncHeightConsistency()
	{
		foreach (var km in GetWallets().Select(x => x.KeyManager).Where(x => x.GetNetwork() == Network))
		{
			km.EnsureTurboSyncHeightConsistency();
		}
	}

	public void SetMaxBestHeight(uint bestHeight)
	{
		foreach (var km in GetWallets().Select(x => x.KeyManager).Where(x => x.GetNetwork() == Network))
		{
			km.SetMaxBestHeight(new Height(bestHeight));
		}
	}

	public Wallet GetWalletByName(string walletName)
	{
		lock (_lock)
		{
			return _wallets.Single(x => x.KeyManager.WalletName == walletName);
		}
	}
}
