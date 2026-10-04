using NBitcoin;
using System.Collections.Concurrent;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Mobile;

public sealed class WalletSession : IAsyncDisposable
{
	private readonly SemaphoreSlim _operations = new(1, 1);
	private readonly CancellationTokenSource _stop = new();
	private readonly ConcurrentDictionary<WalletId, TaskCompletionSource> _mixing = new();
	private readonly ConcurrentDictionary<WalletId, Task> _walletStarts = new();
	private readonly string _dataDir;
	private readonly string _torDirectory;
	private readonly Config _config;
	private bool _initialized;
	private int _disposed;
	private (BuildTransactionResult Result, string Hex, WalletId Wallet, DateTime Created)? _reviewed;
	public string CoinJoinStatus { get; private set; } = "Idle";
	public string? SynchronizationError { get; private set; }

	public WalletSession(string dataDir, MobileSettings settings, string torDirectory)
	{
		settings.Validate();
		Settings = settings;
		_dataDir = dataDir;
		_torDirectory = torDirectory;
		Directory.CreateDirectory(dataDir);
		var defaults = settings.GetNetwork() == Network.RegTest
			? PersistentConfigManager.DefaultRegTestConfig
			: PersistentConfigManager.DefaultMainNetConfig;
		var persistent = defaults with
		{
			Network = settings.GetNetwork(),
			CoordinatorUri = settings.Coordinator,
			CoordinatorIdentifier = settings.CoordinatorIdentifier,
			BitcoinRpcUri = settings.BitcoinRpcUri,
			BitcoinRpcCredentialString = settings.BitcoinRpcCredentials,
			ExchangeRateProvider = settings.GetNetwork() == Network.RegTest ? "None" : defaults.ExchangeRateProvider,
			FeeRateEstimationProvider = settings.GetNetwork() == Network.RegTest ? "None" : defaults.FeeRateEstimationProvider,
			UseTor = settings.GetNetwork() == Network.RegTest ? "Disabled" : "EnabledOnlyRunning",
			DownloadNewVersion = false,
			TerminateTorOnExit = false,
			JsonRpcServerEnabled = false,
			ExperimentalFeatures = [],
			AbsoluteMinInputCount = settings.GetNetwork() == Network.RegTest ? 2 : defaults.AbsoluteMinInputCount
		};
		_config = new Config(persistent, [$"--torfolder={torDirectory}", "--torsocksport=37154", "--torcontrolport=37155"]);
		Global = new Global(dataDir, _config, torDirectory);
	}

	public Global Global { get; private set; }
	public MobileSettings Settings { get; }
	public Wallet? Current { get; private set; }
	public bool IsMixing => !_mixing.IsEmpty;
	public bool IsUnlocked => Current?.IsLoggedIn is true;
	public bool IsReady => _initialized;
	public bool IsSynchronized => _initialized && Current is { Loaded: true } wallet
		&& Global.FilterHeaders.HashCount > 0
		&& wallet.KeyManager.GetBestHeight() >= Global.FilterHeaders.TipHeight
		&& Global.FilterHeaders.HashesLeft == 0
		&& Global.GetPeerCount() > 0;

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!_initialized) { await InitializeNoLockAsync(cancellationToken).ConfigureAwait(false); }
		}
		finally { _operations.Release(); }
	}

	private async Task InitializeNoLockAsync(CancellationToken cancellationToken)
	{
		var terminate = new TerminateService(() => Task.CompletedTask, () => { });
		await Global.InitializeAsync(false, terminate, cancellationToken).ConfigureAwait(false);
		if (Global.Config.TryGetCoordinatorUri(out _))
		{
			Global.HostedServices.Get<CoinJoinManager>().StatusChanged += OnCoinJoinStatusChanged;
		}
		_initialized = true;
		foreach (var wallet in Global.WalletManager.GetWallets()) { StartWallet(wallet); }
	}

	private void StartWallet(Wallet wallet)
	{
		var global = Global;
		_walletStarts.GetOrAdd(wallet.WalletId, _ => Task.Run(async () =>
		{
			try { await global.WalletManager.StartWalletAsync(wallet).ConfigureAwait(false); }
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				Logger.LogError(ex);
				SynchronizationError = "Wallet synchronization failed. Reconnect in Settings to retry.";
			}
		}));
	}

	private async Task RecheckpointAsync()
	{
		// An imported wallet can predate the filter cache. Recreate the engine so
		// it chooses the earliest wallet birthday before starting its filter feed.
		_initialized = false;
		var wallets = Global.WalletManager.GetWallets().ToArray();
		await Global.DisposeAsync().ConfigureAwait(false);
		await Task.WhenAll(_walletStarts.Values).ConfigureAwait(false);
		foreach (var wallet in wallets) { wallet.ClearSensitiveKeys(); }
		Current = null;
		_reviewed = null;
		_walletStarts.Clear();
		SynchronizationError = null;
		Global = new Global(_dataDir, _config, _torDirectory);
		await InitializeNoLockAsync(_stop.Token).ConfigureAwait(false);
	}

	public async Task<Wallet> CreateAsync(string name, string password, Mnemonic mnemonic, bool recover)
	{
		await _operations.WaitAsync(_stop.Token).ConfigureAwait(false);
		try
		{
			if (IsMixing) { throw new InvalidOperationException("Stop CoinJoin before adding a wallet."); }
			if (Global.WalletManager.ValidateWalletName(name) is { } error)
			{
				throw new ArgumentException(error.Message);
			}
			if (!mnemonic.IsValidChecksum)
			{
				throw new FormatException("The recovery words have an invalid checksum.");
			}
			PasswordHelper.Guard(password);
			var path = Global.WalletManager.WalletDirectories.GetWalletFilePaths(name);
			var manager = recover
				? KeyManager.Recover(mnemonic, password, Global.Network,
					KeyManager.GetAccountKeyPath(Global.Network, ScriptPubKeyType.Segwit),
					KeyManager.GetAccountKeyPath(Global.Network, ScriptPubKeyType.TaprootBIP86), path)
				: KeyManager.CreateNew(mnemonic, password, Global.Network, path);
			if (!recover && Global.FilterHeaders.HashCount > 0)
			{
				manager.SetBestHeight(Global.FilterHeaders.TipHeight);
			}
			var wallet = Global.WalletManager.AddWallet(manager);
			if (_initialized && Global.FilterStore.GetMinimumBlockHeight() is { } minimum
				&& FilterCheckpoints.GetCheckpointForBirthday(manager.GetBestHeight(), Global.Network).Header.Height < minimum)
			{
				await RecheckpointAsync().ConfigureAwait(false);
				wallet = Global.WalletManager.GetWalletByName(name);
			}
			else if (_initialized) { StartWallet(wallet); }
			Unlock(wallet, password);
			return wallet;
		}
		finally
		{
			_operations.Release();
		}
	}

	public async Task<Wallet> ImportAsync(string name, string encryptedJson)
	{
		if (System.Text.Encoding.UTF8.GetByteCount(encryptedJson) > 4 * 1024 * 1024)
		{
			throw new FormatException("The wallet backup is too large.");
		}
		await _operations.WaitAsync(_stop.Token).ConfigureAwait(false);
		var temporary = Path.Combine(_dataDir, "import-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			if (IsMixing) { throw new InvalidOperationException("Stop CoinJoin before importing a wallet."); }
			if (Global.WalletManager.ValidateWalletName(name) is { } error) { throw new ArgumentException(error.Message); }
			await File.WriteAllTextAsync(temporary, encryptedJson, _stop.Token).ConfigureAwait(false);
			var manager = KeyManager.FromFile(temporary);
			if (manager.GetNetwork() != Global.Network) { throw new FormatException("Select the backup's Bitcoin network in Settings first."); }
			// A wallet JSON does not include its transaction database. Its saved
			// scan position cannot be reused when importing onto another device.
			manager.SetBestHeight(FilterCheckpoints.GetWasabiGenesisFilter(Global.Network).Header.Height, toFile: false);
			manager.AutoCoinJoin = false;
			manager.SetFilePath(Global.WalletManager.WalletDirectories.GetWalletFilePaths(name));
			manager.ToFile();
			var wallet = Global.WalletManager.AddWallet(manager);
			if (_initialized && Global.FilterStore.GetMinimumBlockHeight() is { } minimum
				&& FilterCheckpoints.GetCheckpointForBirthday(manager.GetBestHeight(), Global.Network).Header.Height < minimum)
			{
				await RecheckpointAsync().ConfigureAwait(false);
				wallet = Global.WalletManager.GetWalletByName(name);
			}
			else if (_initialized) { StartWallet(wallet); }
			return wallet;
		}
		finally
		{
			File.Delete(temporary);
			_operations.Release();
		}
	}

	public void Unlock(Wallet wallet, string password)
	{
		if (Current is { } previous && previous != wallet)
		{
			Lock();
		}
		if (!wallet.KeyManager.IsWatchOnly && !PasswordHelper.TryPassword(wallet.KeyManager, password, out _)
			|| !wallet.TryLogin(password, out _))
		{
			throw new UnauthorizedAccessException("Incorrect wallet password.");
		}
		Current = wallet;
	}

	public void Lock()
	{
		if (IsMixing)
		{
			throw new InvalidOperationException("Stop CoinJoin before locking the signing keys.");
		}
		if (Current is { } wallet)
		{
			wallet.ClearSensitiveKeys();
		}
		Current = null;
		_reviewed = null;
	}

	public string Receive(string label, ScriptPubKeyType type = ScriptPubKeyType.TaprootBIP86)
	{
		var wallet = RequireWallet();
		if (type == ScriptPubKeyType.TaprootBIP86 && wallet.KeyManager.TaprootExtPubKey is null) { type = ScriptPubKeyType.Segwit; }
		// A blank label would leave the address eligible for reuse in KeyManager.
		var key = wallet.GetNextReceiveAddress([string.IsNullOrWhiteSpace(label) ? "Receive" : label], type);
		wallet.KeyManager.ToFile();
		return (type == ScriptPubKeyType.Segwit ? key.P2wpkhScript : key.P2Taproot).GetDestinationAddress(Global.Network)!.ToString();
	}

	public BuildTransactionResult Preview(PaymentRequest request, Money amount, FeeRate rate, OutPoint[]? selectedCoins, bool sendAll = false)
	{
		var wallet = RequireWallet();
		if (IsMixing) { throw new InvalidOperationException("Stop CoinJoin before preparing a payment."); }
		if (!IsSynchronized)
		{
			throw new InvalidOperationException("Wait for the wallet to finish synchronizing.");
		}
		var available = wallet.GetAllCoins().Unspent().Where(c => c.IsAvailable()).Select(c => c.Outpoint).ToHashSet();
		var allowed = selectedCoins is null ? available.ToArray() : selectedCoins.Where(available.Contains).ToArray();
		var intent = sendAll
			? new PaymentIntent(request.Address, MoneyRequest.CreateAllRemaining(), new LabelsArray(request.Label))
			: new PaymentIntent(request.Address.ScriptPubKey, amount, label: new LabelsArray(request.Label));
		var result = new TransactionFactory(Global.Network, wallet.KeyManager, wallet.GetAllCoins(), Global.TransactionStore, wallet.Password)
			.BuildTransaction(new(intent, rate, false, false, allowed, false, false));
		_reviewed = (result, result.Psbt.GetGlobalTransaction().ToHex(), wallet.WalletId, DateTime.UtcNow);
		return result;
	}

	public async Task<string> SendAsync(BuildTransactionResult preview, string password, CancellationToken cancellationToken)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var wallet = RequireWallet();
			if (_reviewed is not { } reviewed || !ReferenceEquals(reviewed.Result, preview) || reviewed.Wallet != wallet.WalletId
				|| DateTime.UtcNow - reviewed.Created > TimeSpan.FromMinutes(5) || preview.Psbt.GetGlobalTransaction().ToHex() != reviewed.Hex || IsMixing)
			{
				throw new InvalidOperationException("Review this transaction again before sending.");
			}
			if (!IsSynchronized || !PasswordHelper.TryPassword(wallet.KeyManager, password, out var compatiblePassword))
			{
				throw new InvalidOperationException("The wallet must be synchronized and the password correct.");
			}
			var available = wallet.GetAllCoins().Unspent().Where(c => c.IsAvailable()).Select(c => c.Outpoint).ToHashSet();
			if (preview.SpentCoins.Any(c => !available.Contains(c.Outpoint)))
			{
				throw new InvalidOperationException("The selected coins changed. Review a new transaction.");
			}
			var psbt = preview.Psbt.Clone();
			var unsigned = psbt.GetGlobalTransaction().ToHex();
			var keys = wallet.KeyManager.GetSecrets(compatiblePassword ?? password, preview.SpentCoins.Select(c => c.ScriptPubKey).ToArray());
			var builder = Global.Network.CreateTransactionBuilder();
			builder.AddCoins(preview.SpentCoins.Select(c => c.Coin));
			builder.AddKeys(keys.ToArray());
			builder.SignPSBT(psbt);
			if (psbt.GetGlobalTransaction().ToHex() != unsigned)
			{
				throw new InvalidOperationException("The transaction changed during signing.");
			}
			psbt.Finalize();
			var transaction = psbt.ExtractTransaction();
			if (builder.Check(transaction).Any())
			{
				throw new InvalidOperationException("Transaction validation failed.");
			}
			var smart = new SmartTransaction(transaction, Height.Mempool);
			_reviewed = null;
			await Global.TransactionBroadcaster.SendTransactionAsync(smart, cancellationToken).ConfigureAwait(false);
			wallet.UpdateUsedHdPubKeysLabels(preview.HdPubKeysWithNewLabels);
			return transaction.GetHash().ToString();
		}
		finally
		{
			_operations.Release();
		}
	}

	public void StartCoinJoin()
	{
		var wallet = RequireWallet();
		if (!IsSynchronized || !Global.Config.TryGetCoordinatorUri(out _))
		{
			throw new InvalidOperationException("Synchronize the wallet and configure a coordinator first.");
		}
		_reviewed = null;
		_mixing.TryAdd(wallet.WalletId, new(TaskCreationOptions.RunContinuationsAsynchronously));
		CoinJoinStatus = "Starting";
		Global.HostedServices.Get<CoinJoinManager>().RequestCoinJoinStart(wallet, wallet, true, false);
	}

	public async Task StopCoinJoinAsync(CancellationToken cancellationToken)
	{
		if (!IsMixing)
		{
			return;
		}
		var manager = Global.HostedServices.Get<CoinJoinManager>();
		var stops = _mixing.Values.Select(tcs => tcs.Task).ToArray();
		foreach (var wallet in Global.WalletManager.GetWallets().Where(w => _mixing.ContainsKey(w.WalletId)))
		{
			manager.RequestCoinJoinStop(wallet);
		}
		CoinJoinStatus = "Stopping safely";
		await Task.WhenAll(stops).WaitAsync(cancellationToken).ConfigureAwait(false);
	}

	private void OnCoinJoinStatusChanged(object? sender, StatusChangedEventArgs e)
	{
		CoinJoinStatus = e switch
		{
			StartErrorEventArgs error => error.Error switch
			{
				CoinjoinError.AllCoinsPrivate => "All coins are private",
				CoinjoinError.MiningFeeRateTooHigh => "Waiting for lower mining fees",
				CoinjoinError.MinInputCountTooLow => "Coordinator round has too few inputs",
				CoinjoinError.CoordinatorLiedAboutInputs => "Coordinator input verification failed",
				CoinjoinError.CoinsRejected => "Coordinator rejected the selected coins",
				CoinjoinError.OnlyExcludedCoinsAvailable => "All eligible coins are excluded",
				CoinjoinError.OnlyImmatureCoinsAvailable or CoinjoinError.NoConfirmedCoinsEligibleToMix => "Waiting for confirmations",
				CoinjoinError.NotEnoughUnprivateBalance or CoinjoinError.NotEnoughConfirmedUnprivateBalance => "Waiting for more eligible funds",
				_ => "Waiting to retry"
			},
			CompletedEventArgs completed => completed.CompletionStatus == CompletionStatus.Success ? "CoinJoin completed" : "Round ended; waiting to retry",
			CoinJoinStatusEventArgs status => status.CoinJoinProgressEventArgs switch
			{
				WaitingForRound => "Waiting for a round",
				WaitingForBlameRound => "Waiting for a recovery round",
				EnteringInputRegistrationPhase => "Registering inputs",
				EnteringConnectionConfirmationPhase => "Confirming participation",
				EnteringOutputRegistrationPhase => "Registering private outputs",
				EnteringSigningPhase => "Signing the CoinJoin",
				TransactionSigned => "Waiting for broadcast",
				_ => CoinJoinStatus
			},
			WalletStoppedCoinJoinEventArgs => "Stopped",
			WalletStartedCoinJoinEventArgs => "Joining a round",
			_ => CoinJoinStatus
		};
		if (e is WalletStoppedCoinJoinEventArgs && _mixing.TryRemove(e.Wallet.WalletId, out var stopped)) { stopped.TrySetResult(); }
	}

	private Wallet RequireWallet() => IsUnlocked && Current is { } wallet ? wallet : throw new InvalidOperationException("Unlock a wallet first.");

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
		await _stop.CancelAsync().ConfigureAwait(false);
		await _operations.WaitAsync().ConfigureAwait(false);
		try
		{
			_initialized = false;
			var wallets = Global.WalletManager.GetWallets().ToArray();
			await Global.DisposeAsync().ConfigureAwait(false);
			await Task.WhenAll(_walletStarts.Values).ConfigureAwait(false);
			foreach (var wallet in wallets) { wallet.ClearSensitiveKeys(); }
			Current = null;
		}
		finally { _operations.Release(); _stop.Dispose(); _operations.Dispose(); }
	}
}
