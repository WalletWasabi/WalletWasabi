using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
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
	private readonly WalletPolicy _policy;
	private readonly bool _regtestNodeConfigured;
	private readonly TransactionJournal _journal;
	private readonly CoinJoinJournal _coinJoinJournal;
	private readonly object _authorizationGate = new();
	private readonly Func<bool> _transportReady;
	private readonly Action<HttpClientHandler>? _configureHttpHandler;
	private bool _interfaceLocked = true;
	private bool _initialized;
	private int _disposed;
	private sealed record Reviewed(PaymentProposal Proposal, BuildTransactionResult Result, string Psbt, WalletId Owner, SmartTransaction? Parent, long CreatedTimestamp);
	private Reviewed? _reviewed;
	private string _coinJoinStatus = "Idle";
	public string CoinJoinStatus
	{
		get => !IsMixing && PendingCoinJoins > 0 ? "Awaiting CoinJoin reconciliation" : _coinJoinStatus;
		private set => _coinJoinStatus = value;
	}
	public int PendingCoinJoins => _coinJoinJournal.Entries.Count(e => e.TransactionId is not null);
	public string? SynchronizationError { get; private set; }

	public WalletSession(string dataDir, MobileSettings settings, string torDirectory, WalletPolicy? policy = null, int socksPort = 37154, Func<bool>? transportReady = null, Action<HttpClientHandler>? configureHttpHandler = null, RegtestNodeOptions? regtestNode = null)
	{
		_configureHttpHandler = configureHttpHandler;
		settings.Validate();
		regtestNode?.Validate(settings.GetNetwork());
		_regtestNodeConfigured = regtestNode is not null;
		_policy = policy ?? WalletPolicy.Development;
		_policy.RequireNetwork(settings.GetNetwork());
		_transportReady = transportReady ?? (() => settings.GetNetwork() == Network.RegTest);
		Settings = settings;
		_dataDir = dataDir;
		_torDirectory = torDirectory;
		Directory.CreateDirectory(dataDir);
		_journal = new(dataDir, settings.GetNetwork());
		_coinJoinJournal = new(dataDir, settings.GetNetwork());
		var defaults = settings.GetNetwork() == Network.RegTest
			? PersistentConfigManager.DefaultRegTestConfig
			: PersistentConfigManager.DefaultMainNetConfig;
		var persistent = defaults with
		{
			Network = settings.GetNetwork(),
			CoordinatorUri = settings.Coordinator,
			CoordinatorIdentifier = settings.CoordinatorIdentifier,
			BitcoinRpcUri = regtestNode?.Uri ?? "",
			BitcoinRpcCredentialString = regtestNode?.Credentials ?? "",
			ExchangeRateProvider = settings.GetNetwork() == Network.RegTest ? "None" : defaults.ExchangeRateProvider,
			FeeRateEstimationProvider = settings.GetNetwork() == Network.RegTest ? "None" : defaults.FeeRateEstimationProvider,
			UseTor = settings.GetNetwork() == Network.RegTest ? "Disabled" : "EnabledOnlyRunning",
			DownloadNewVersion = false,
			TerminateTorOnExit = false,
			JsonRpcServerEnabled = false,
			ExperimentalFeatures = [],
			AbsoluteMinInputCount = settings.GetNetwork() == Network.RegTest ? 2 : defaults.AbsoluteMinInputCount
		};
		_config = new Config(persistent, [$"--torfolder={torDirectory}", $"--torsocksport={socksPort}", $"--torcontrolport={socksPort + 1}"]);
		Global = new Global(dataDir, _config, torDirectory, _configureHttpHandler);
	}

	public Global Global { get; private set; }
	public MobileSettings Settings { get; }
	public Wallet? Current { get; private set; }
	public bool IsMixing => !_mixing.IsEmpty;
	public bool IsUnlocked => !_interfaceLocked && Current is not null;
	public ImmutableArray<BroadcastReceipt> PendingTransactions => _journal.Entries.Select(e => new BroadcastReceipt(e.ProposalId, e.TransactionId, e.State)).ToImmutableArray();
	public ImmutableArray<SubmissionDetails> SubmissionHistory => Current is { } wallet
		? _journal.Entries.Where(e => e.WalletId == AccountId(wallet)).Select(e => new SubmissionDetails(e.TransactionId, e.Operation, e.State,
			e.AmountSatoshis, e.FeeSatoshis, e.CreatedAt, e.Outputs.IsDefault ? [] : e.Outputs)).ToImmutableArray() : [];
	public bool IsReady => _initialized;
	public SynchronizationSnapshot GetSynchronizationSnapshot(int torBootstrap)
	{
		var wallet = Current;
		var target = Global.FilterHeaders.ServerTipHeight.Height;
		var headers = _regtestNodeConfigured ? Global.FilterHeaders.TipHeight.Height : Global.GetBlockHeadersTipHeight();
		return new(Global.Network.Name, IsReady, torBootstrap, Global.GetPeerCount(), headers,
			target > 0 || Global.FilterHeaders.IsSynchronized ? target : null, Global.FilterHeaders.TipHeight.Height,
			wallet?.KeyManager.GetBestHeight().Height, wallet?.Loaded is true, IsSynchronized,
			wallet is null ? null : WalletReference(wallet), SynchronizationError);
	}
	public bool IsSynchronized => Current is { } wallet && IsSynchronizedWallet(wallet);
	private bool IsSynchronizedWallet(Wallet wallet) => _initialized && wallet.Loaded
		&& _transportReady()
		&& Global.FilterHeaders.IsSynchronized
		&& wallet.KeyManager.GetBestHeight() >= Global.FilterHeaders.TipHeight
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
		wallet.CoinJoinCheckpoints = _coinJoinJournal.ForWallet(AccountId(wallet), wallet.KeyManager.ToFile, input => _journal.Reservations().Contains(input));
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
		Global = new Global(_dataDir, _config, _torDirectory, _configureHttpHandler);
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
			try { File.Delete(temporary); }
			finally { _operations.Release(); }
		}
	}

	public void Unlock(Wallet wallet, string password)
	{
		lock (_authorizationGate)
		{
		if (IsMixing && Current != wallet) { throw new InvalidOperationException("Stop CoinJoin before switching wallets."); }
		_policy.RequireNetwork(wallet.Network);
		if (wallet.Network != Global.Network || !Global.WalletManager.GetWallets().Contains(wallet)) { throw new InvalidOperationException("This wallet does not belong to this session."); }
		if (Current is { } previous && previous != wallet)
		{
			Lock();
		}
		try
		{
		if (!wallet.KeyManager.IsWatchOnly && !PasswordHelper.TryPassword(wallet.KeyManager, password, out _)
			|| !wallet.TryLogin(password, out _))
		{
			throw new UnauthorizedAccessException("Incorrect wallet password.");
		}
		if (!wallet.KeyManager.IsWatchOnly)
		{
			var manager = wallet.KeyManager;
			var master = manager.GetMasterExtKey(wallet.Password);
			if (master.Derive(manager.SegwitAccountKeyPath).Neuter() != manager.SegwitExtPubKey
				|| manager.TaprootExtPubKey is not null && master.Derive(manager.TaprootAccountKeyPath).Neuter() != manager.TaprootExtPubKey)
			{
				wallet.ClearSensitiveKeys();
				throw new FormatException("The backup's account keys do not match its encrypted signing key.");
			}
		}
		Current = wallet;
		_interfaceLocked = false;
		}
		finally { if (!IsMixing) { wallet.ClearSensitiveKeys(); } }
		}
	}

	public void Lock(bool preserveProposal = false)
	{
		lock (_authorizationGate)
		{
		_interfaceLocked = true;
		if (!preserveProposal) { _reviewed = null; }
		if (IsMixing)
		{
			// Only the explicitly authorized manager retains credentials. UI operations
			// still require unlocking and a fresh password/Keystore authorization.
			return;
		}
		if (Current is { } wallet)
		{
			wallet.ClearSensitiveKeys();
		}
		Current = null;
		if (!preserveProposal) { _reviewed = null; }
		}
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

	public async Task<PaymentProposal> PrepareAsync(PaymentRequest request, Money amount, FeeRate rate, OutPoint[]? selectedCoins, bool sendAll = false, CancellationToken cancellationToken = default)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var wallet = RequirePreparedWallet();
			if (request.Address.Network != Global.Network) { throw new FormatException("The payment belongs to a different Bitcoin network."); }
			var reserved = ReservedInputs();
			var available = wallet.GetAllCoins().Unspent().Where(c => c.IsAvailable() && !reserved.Contains(c.Outpoint)).Select(c => c.Outpoint).ToHashSet();
			if (selectedCoins?.Any(c => !available.Contains(c)) is true) { throw new InvalidOperationException("Some selected coins are unavailable or reserved."); }
			var allowed = selectedCoins ?? available.ToArray();
			var intent = sendAll
				? new PaymentIntent(request.Address, MoneyRequest.CreateAllRemaining(), new LabelsArray(request.Label))
				: new PaymentIntent(request.Address.ScriptPubKey, amount, label: new LabelsArray(request.Label));
			var result = new TransactionFactory(Global.Network, wallet.KeyManager, wallet.GetAllCoins(), Global.TransactionStore, "")
				.BuildTransaction(new(intent, rate, false, false, allowed, false, false));
			var sent = result.Transaction.Transaction.Outputs.Where(o => o.ScriptPubKey == request.Address.ScriptPubKey).Sum(o => o.Value.Satoshi);
			return RegisterProposal(wallet, result, PaymentOperation.Payment, sent, null, new HashSet<Script> { request.Address.ScriptPubKey });
		}
		finally { _operations.Release(); }
	}

	public async Task<PaymentProposal> PrepareReplacementAsync(string transactionId, PaymentOperation operation, FeeRate? feeRate, CancellationToken cancellationToken)
	{
		if (operation == PaymentOperation.Payment) { throw new ArgumentException("Choose speed-up or cancellation."); }
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var wallet = RequirePreparedWallet();
			if (!Global.TransactionStore.TryGetTransaction(uint256.Parse(transactionId), out var parent) || parent.Confirmed
				|| !wallet.GetTransactions().Any(t => t.GetHash() == parent.GetHash())) { throw new InvalidOperationException("This pending transaction is no longer eligible."); }
			if (operation == PaymentOperation.SpeedUp && parent.TryGetLargestCPFP(wallet.KeyManager, out var child)) { parent = child; }
			var originalSubmission = _journal.Entries.FirstOrDefault(e => e.TransactionId == parent.GetHash().ToString());
			// A destination belonging to this wallet is still an approved payment.
			// Older journals do not identify such destinations. Protect all their
			// wallet outputs conservatively and use CPFP if RBF has no safe change.
			var protectedWalletRecipients = originalSubmission is not null && !originalSubmission.Outputs.IsDefault && (originalSubmission.Outputs.Any(o => o.IsRecipient) || originalSubmission.Operation != PaymentOperation.Payment)
				? originalSubmission.Outputs.Where(o => o.IsRecipient && o.IsWalletOutput).Select(o => Script.FromHex(o.ScriptHex)).ToHashSet()
				: parent.GetWalletOutputs(wallet.KeyManager).Select(o => o.ScriptPubKey).ToHashSet();
			var recipients = parent.GetForeignOutputs(wallet.KeyManager).Select(o => o.TxOut.ScriptPubKey).Concat(protectedWalletRecipients).ToHashSet();
			var uncertain = _journal.Entries.Where(e => e.State == SubmissionState.Uncertain).SelectMany(e => Transaction.Parse(e.Hex, Global.Network).Inputs.Select(i => i.PrevOut)).ToHashSet();
			if (parent.WalletInputs.Any(c => uncertain.Contains(c.Outpoint))) { throw new InvalidOperationException("Reconcile the interrupted submission before replacing it."); }
			var result = operation == PaymentOperation.Cancel
				? wallet.CancelTransaction(parent, tryToSign: false)
				: await wallet.SpeedUpTransactionAsync(parent, feeRate, cancellationToken, tryToSign: false, preserveRecipients: true, protectedWalletRecipients: protectedWalletRecipients).ConfigureAwait(false);
			// A CPFP keeps the parent intact. An RBF must preserve every recipient,
			// including destinations owned by this wallet.
			if (operation == PaymentOperation.SpeedUp && result.SpentCoins.Any(c => parent.WalletInputs.Contains(c)))
			{
				var original = parent.Transaction.Outputs.Where(o => recipients.Contains(o.ScriptPubKey)).Select(o => (o.ScriptPubKey.ToHex(), o.Value.Satoshi)).OrderBy(o => o.Item1).ThenBy(o => o.Satoshi);
				var replacement = result.Transaction.Transaction.Outputs.Where(o => recipients.Contains(o.ScriptPubKey)).Select(o => (o.ScriptPubKey.ToHex(), o.Value.Satoshi)).OrderBy(o => o.Item1).ThenBy(o => o.Satoshi);
				if (!original.SequenceEqual(replacement)) { throw new InvalidOperationException("This replacement would change the approved recipients."); }
			}
			var replacementRecipients = operation == PaymentOperation.SpeedUp && result.SpentCoins.Any(c => parent.WalletInputs.Contains(c)) ? recipients : new HashSet<Script>();
			var amount = result.Transaction.Transaction.Outputs.Where(o => replacementRecipients.Contains(o.ScriptPubKey)).Sum(o => o.Value.Satoshi);
			return RegisterProposal(wallet, result, operation, amount, parent, replacementRecipients);
		}
		finally { _operations.Release(); }
	}

	private Wallet RequirePreparedWallet()
	{
		var wallet = RequireWallet();
		if (wallet.KeyManager.IsWatchOnly) { throw new InvalidOperationException("A watch-only wallet cannot sign payments or CoinJoins."); }
		if (IsMixing) { throw new InvalidOperationException("Stop CoinJoin before preparing a payment."); }
		if (!IsSynchronized) { throw new InvalidOperationException("Wait for the wallet to finish synchronizing."); }
		return wallet;
	}

	private PaymentProposal RegisterProposal(Wallet wallet, BuildTransactionResult result, PaymentOperation operation, long amount, SmartTransaction? parent, HashSet<Script> recipients)
	{
		if (result.Signed) { throw new InvalidOperationException("Preparation unexpectedly signed the transaction."); }
		var tx = result.Psbt.GetGlobalTransaction();
		var own = result.InnerWalletOutputs.Select(c => c.ScriptPubKey).ToHashSet();
		var proposal = new PaymentProposal(Guid.NewGuid().ToString("N"), AccountId(wallet), Global.Network.Name, operation,
			result.SpentCoins.Select(c => new ProposalInput(c.Outpoint.Hash.ToString(), c.Outpoint.N, c.Amount.Satoshi)).ToImmutableArray(),
			tx.Outputs.Select(o => new ProposalOutput(o.ScriptPubKey.ToHex(), o.ScriptPubKey.GetDestinationAddress(Global.Network)?.ToString(), o.Value.Satoshi, own.Contains(o.ScriptPubKey), recipients.Contains(o.ScriptPubKey))).ToImmutableArray(),
			amount, result.Fee.Satoshi, checked(amount + result.Fee.Satoshi), DateTimeOffset.UtcNow.AddMinutes(5), parent?.GetHash().ToString());
		wallet.KeyManager.ToFile(); // Persist allocated change before revealing or registering it.
		lock (_authorizationGate)
		{
			if (!IsUnlocked || Current != wallet) { throw new InvalidOperationException("Unlock the wallet and review again."); }
			_reviewed = new(proposal, result, result.Psbt.ToBase64(), wallet.WalletId, parent, Stopwatch.GetTimestamp());
		}
		return proposal;
	}

	// Legacy backups acquire a Taproot account on their first successful unlock.
	// Their vault/journal identity must remain stable across that migration.
	private static string AccountId(Wallet wallet) => WalletAccountIdentity.Reference(wallet.Network, wallet.KeyManager.SegwitAccountKeyPath, wallet.KeyManager.SegwitExtPubKey);
	private static string LegacyAccountId(Wallet wallet) => WalletAccountIdentity.LegacyReference(wallet.Network, wallet.KeyManager.SegwitAccountKeyPath);
	public static string WalletReference(Wallet wallet) => AccountId(wallet);

	public async Task<byte[]> ExportEncryptedBackupAsync(string password, CancellationToken cancellationToken)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			lock (_authorizationGate)
			{
				var wallet = RequireWallet();
				try
				{
				if (!wallet.KeyManager.IsWatchOnly && !PasswordHelper.TryPassword(wallet.KeyManager, password, out _)) { throw new UnauthorizedAccessException("Incorrect wallet password."); }
				wallet.KeyManager.ToFile();
				return File.ReadAllBytes(wallet.KeyManager.FilePath!);
				}
				finally { if (!IsMixing) { wallet.ClearSensitiveKeys(); } }
			}
		}
		finally { _operations.Release(); }
	}

	public async Task<BroadcastReceipt> ConfirmAsync(string proposalId, string password, CancellationToken cancellationToken)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_journal.Entries.FirstOrDefault(e => e.ProposalId == proposalId) is { } alreadySubmitted) { return Receipt(alreadySubmitted); }
			JournalEntry entry;
			Wallet wallet;
			lock (_authorizationGate)
			{
				wallet = RequirePreparedWallet();
				try
				{
				if (_reviewed is not { } reviewed || reviewed.Proposal.Id != proposalId || reviewed.Owner != wallet.WalletId
					|| reviewed.Proposal.WalletId != AccountId(wallet) || reviewed.Proposal.Network != Global.Network.Name
					|| Stopwatch.GetElapsedTime(reviewed.CreatedTimestamp) >= TimeSpan.FromMinutes(5) || reviewed.Result.Psbt.ToBase64() != reviewed.Psbt)
				{ throw new InvalidOperationException("Review this transaction again before sending."); }
				cancellationToken.ThrowIfCancellationRequested();
				if (!PasswordHelper.TryPassword(wallet.KeyManager, password, out var compatiblePassword)) { throw new UnauthorizedAccessException("Incorrect wallet password."); }
				var preview = reviewed.Result;
				if (reviewed.Parent is { } parent && (!Global.TransactionStore.TryGetTransaction(parent.GetHash(), out var currentParent) || currentParent.Confirmed))
				{ throw new InvalidOperationException("The original transaction changed. Review again."); }
				var reserved = ReservedInputs();
				if (preview.SpentCoins.Any(c => c.CoinJoinInProgress ||
					!(c.IsAvailable() && !reserved.Contains(c.Outpoint) || reviewed.Parent is { } original && c.SpenderTransaction?.GetHash() == original.GetHash() && !original.Confirmed)))
				{ throw new InvalidOperationException("The selected inputs changed or are reserved. Review again."); }
				var psbt = preview.Psbt.Clone();
				var unsignedTransaction = psbt.GetGlobalTransaction();
				// The engine prepares immediate, height-locked payments. A reorg
				// after review can make the reviewed locktime non-final again.
				if (unsignedTransaction.LockTime.Value > Global.FilterHeaders.TipHeight)
				{ throw new InvalidOperationException("The chain changed after review. Review this transaction again."); }
				var unsigned = unsignedTransaction.ToHex();
				var keys = new List<Key>();
				Transaction transaction;
				try
				{
					keys.AddRange(wallet.KeyManager.GetSecrets(compatiblePassword ?? password, preview.SpentCoins.Select(c => c.ScriptPubKey).ToArray()));
					var builder = Global.Network.CreateTransactionBuilder();
					builder.AddCoins(preview.SpentCoins.Select(c => c.Coin));
					builder.AddKeys(keys.ToArray());
					builder.SignPSBT(psbt);
					if (psbt.GetGlobalTransaction().ToHex() != unsigned) { throw new InvalidOperationException("The transaction changed during signing."); }
					psbt.Finalize();
					transaction = psbt.ExtractTransaction();
					if (builder.Check(transaction).Any()) { throw new InvalidOperationException("Transaction validation failed."); }
				}
				finally { foreach (var key in keys) { key.Dispose(); } }
				entry = new(proposalId, AccountId(wallet), Global.Network.Name, reviewed.Proposal.Operation, reviewed.Proposal.OriginalTransactionId,
					transaction.GetHash().ToString(), transaction.ToHex(), SubmissionState.Uncertain, DateTimeOffset.UtcNow,
					reviewed.Proposal.AmountSatoshis, reviewed.Proposal.FeeSatoshis, reviewed.Proposal.Outputs);
				_journal.Put(entry); // Never broadcast before the exact signed bytes are durable.
				_reviewed = null;
				wallet.UpdateUsedHdPubKeysLabels(preview.HdPubKeysWithNewLabels);
				wallet.KeyManager.ToFile();
				}
				finally { wallet.ClearSensitiveKeys(); }
			}
			return await SubmitNoLockAsync(entry, cancellationToken).ConfigureAwait(false);
		}
		finally { _operations.Release(); }
	}

	private async Task<BroadcastReceipt> SubmitNoLockAsync(JournalEntry entry, CancellationToken cancellationToken)
	{
		var smart = new SmartTransaction(Transaction.Parse(entry.Hex, Global.Network), Height.Mempool);
		if (entry.Operation == PaymentOperation.Cancel) { smart.SetCancellation(); }
		if (entry.Operation == PaymentOperation.SpeedUp) { smart.SetSpeedup(); }
		try
		{
			await Global.TransactionBroadcaster.SendTransactionAsync(smart, cancellationToken).ConfigureAwait(false);
			entry = entry with { State = SubmissionState.Pending };
			_journal.Put(entry);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			// A failed response does not prove rejection. Retain bytes and reservations.
			Logger.LogWarning("Transaction submission needs reconciliation.");
		}
		return Receipt(entry);
	}

	private static BroadcastReceipt Receipt(JournalEntry entry) => new(entry.ProposalId, entry.TransactionId, entry.State);
	private HashSet<OutPoint> ReservedInputs() => _journal.Reservations().Concat(_coinJoinJournal.Reservations()).ToHashSet();

	private void MigrateLegacyJournalOwners()
	{
		// The old reference was shared by different keys. Never assign its records
		// by name or by first match. Require ownership of every reserved input after
		// synchronization, and keep ambiguous records and reservations unchanged.
		var accounts = Global.WalletManager.GetWallets().Where(IsSynchronizedWallet)
			.Select(w => new WalletAccountIdentity.Ownership(AccountId(w), LegacyAccountId(w), w.GetAllCoins().Select(c => c.Outpoint).ToHashSet())).ToArray();
		foreach (var entry in _journal.Entries)
		{
			var inputs = Transaction.Parse(entry.Hex, Global.Network).Inputs.Select(i => i.PrevOut);
			if (WalletAccountIdentity.ResolveLegacyOwner(entry.WalletId, inputs, accounts) is { } owner)
			{ _journal.Put(entry with { WalletId = owner }); }
		}
		if (!IsMixing)
		{
			foreach (var checkpoint in _coinJoinJournal.Entries)
			{
				var inputs = checkpoint.Inputs.Select(i => new OutPoint(uint256.Parse(i.TransactionId), i.Index));
				if (WalletAccountIdentity.ResolveLegacyOwner(checkpoint.WalletId, inputs, accounts) is { } owner)
				{ _coinJoinJournal.ReassignWallet(checkpoint, owner); }
			}
		}
	}

	public async Task ReconcilePendingAsync(CancellationToken cancellationToken)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			MigrateLegacyJournalOwners();
			if (!IsMixing)
			{
				foreach (var checkpoint in _coinJoinJournal.Entries)
				{
					var owner = Global.WalletManager.GetWallets().FirstOrDefault(w => AccountId(w) == checkpoint.WalletId && w.Loaded);
					if (owner is null || !IsSynchronizedWallet(owner)) { continue; }
					if (checkpoint.TransactionId is null || Global.TransactionStore.TryGetTransaction(uint256.Parse(checkpoint.TransactionId), out _)
						|| checkpoint.Inputs.All(i => owner.GetAllCoins().Any(c => c.Outpoint == new OutPoint(uint256.Parse(i.TransactionId), i.Index) && c.SpenderTransaction is { Confirmed: true })))
					{ _coinJoinJournal.Remove(checkpoint); }
				}
			}
			foreach (var entry in _journal.Entries)
			{
				var wallet = Global.WalletManager.GetWallets().FirstOrDefault(w => AccountId(w) == entry.WalletId && w.Loaded);
				if (wallet is null || !IsSynchronizedWallet(wallet)) { continue; }
				var inputs = Transaction.Parse(entry.Hex, Global.Network).Inputs.Select(i => i.PrevOut).ToHashSet();
				var conflict = wallet.GetAllCoins().FirstOrDefault(c => inputs.Contains(c.Outpoint) && c.SpenderTransaction is { } spender && spender.GetHash().ToString() != entry.TransactionId)?.SpenderTransaction;
				var replacement = conflict is not null && _journal.Entries.Any(e => e.TransactionId == conflict.GetHash().ToString() && e.OriginalTransactionId == entry.TransactionId);
				if (conflict is not null && (conflict.Confirmed || replacement))
				{
					_journal.Put(entry with { State = replacement ? SubmissionState.Replaced : SubmissionState.Conflicted });
					continue;
				}
				if (Global.TransactionStore.TryGetTransaction(uint256.Parse(entry.TransactionId), out var known))
				{
					_journal.Put(entry with { State = known.Confirmed ? SubmissionState.Confirmed : SubmissionState.Pending });
					continue;
				}
				var retry = entry with { State = SubmissionState.Uncertain };
				_journal.Put(retry);
				await SubmitNoLockAsync(retry, cancellationToken).ConfigureAwait(false);
			}
		}
		finally { _operations.Release(); }
	}

	public async Task StartCoinJoinAsync(string password, CancellationToken cancellationToken = default)
	{
		await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			lock (_authorizationGate)
			{
				var wallet = RequirePreparedWallet();
				if (!Global.Config.TryGetCoordinatorUri(out _)) { throw new InvalidOperationException("Configure a coordinator first."); }
				if (_journal.Entries.Any(e => e.State == SubmissionState.Uncertain)) { throw new InvalidOperationException("Reconcile interrupted payments before starting CoinJoin."); }
				if (_coinJoinJournal.Entries.Any(e => e.WalletId == AccountId(wallet) || e.WalletId == LegacyAccountId(wallet))) { throw new InvalidOperationException("Reconcile the interrupted CoinJoin before starting another round."); }
				if (!PasswordHelper.TryPassword(wallet.KeyManager, password, out var compatiblePassword) || !wallet.TryLogin(compatiblePassword ?? password, out _)) { throw new UnauthorizedAccessException("Incorrect wallet password."); }
				_reviewed = null;
				wallet.KeyManager.ToFile();
				_mixing.TryAdd(wallet.WalletId, new(TaskCreationOptions.RunContinuationsAsynchronously));
				CoinJoinStatus = "Starting";
				Global.HostedServices.Get<CoinJoinManager>().RequestCoinJoinStart(wallet, wallet, true, false);
			}
		}
		finally { _operations.Release(); }
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
		if (e is WalletStoppedCoinJoinEventArgs)
		{
			lock (_authorizationGate)
			{
				e.Wallet.ClearSensitiveKeys();
				if (Current == e.Wallet) { _interfaceLocked = true; _reviewed = null; }
			}
			if (_mixing.TryRemove(e.Wallet.WalletId, out var stopped)) { stopped.TrySetResult(); }
		}
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
