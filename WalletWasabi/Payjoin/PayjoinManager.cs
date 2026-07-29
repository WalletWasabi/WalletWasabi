using NBitcoin;
using Payjoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Monitor = Payjoin.Monitor;
using OutPoint = NBitcoin.OutPoint;

namespace WalletWasabi.Payjoin;

/// <summary>
/// Runs BIP 77 (async payjoin) receiver sessions across app restarts.
///
/// Modeled on <see cref="WabiSabi.Client.CoinJoinManager"/>: a hosted background service
/// that polls the payjoin directory on a fixed cadence and drives each active session's
/// payjoin-ffi typestate chain. Every transition is persisted before it is acted upon
/// (the ffi's <c>Save</c> contract over <see cref="SqliteReceiverSessionPersister"/>), so
/// a kill at any point resumes at the exact state via event-log replay — replay is eager,
/// on the first tick after startup.
///
/// The ffi is a pure state machine: all bytes flow through Wasabi's HTTP client factory
/// (Tor stream isolation per session), and wallet decisions flow through
/// <see cref="PayjoinWalletCallbacks"/>.
/// </summary>
public class PayjoinManager : PeriodicRunner
{
	/// <summary>Directory long-polls hold idle connections; a local bound distinguishes "no proposal yet" from a dead relay.</summary>
	private static readonly TimeSpan PollRequestTimeout = TimeSpan.FromSeconds(10);

	private static readonly TimeSpan RelayCooldown = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan OhttpKeysCacheDuration = TimeSpan.FromHours(12);
	private const int MaxConcurrentSessions = 4;

	public PayjoinManager(
		string dataDir,
		Network network,
		PayjoinConfiguration configuration,
		WabiSabi.Client.CoinJoin.Manager.WalletProvider getWallets,
		IHttpClientFactory httpClientFactory,
		TransactionBroadcaster transactionBroadcaster,
		Func<System.Uri, IHttpClientFactory>? relayHttpClientFactoryBuilder = null)
		: base(TimeSpan.FromSeconds(5))
	{
		_network = network;
		_configuration = configuration;
		_getWalletsAsync = getWallets;
		_httpClientFactory = httpClientFactory;
		_transactionBroadcaster = transactionBroadcaster;
		_relayHttpClientFactoryBuilder = relayHttpClientFactoryBuilder ?? (relayUri => new RelayHttpClientFactory(relayUri));

		string storeDir = System.IO.Path.Combine(dataDir, "Payjoin", network.ToString());
		System.IO.Directory.CreateDirectory(storeDir);
		SessionStore = PayjoinSessionStore.FromFile(System.IO.Path.Combine(storeDir, "Sessions.sqlite"));
	}

	public event EventHandler<PayjoinSessionState>? SessionStatusChanged;

	private readonly Network _network;
	private readonly PayjoinConfiguration _configuration;
	private readonly WabiSabi.Client.CoinJoin.Manager.WalletProvider _getWalletsAsync;
	private readonly IHttpClientFactory _httpClientFactory;

	/// <summary>
	/// Builds a relay-proxied HTTP factory for the Tor-off OHTTP-keys bootstrap: the chosen
	/// relay is set as a CONNECT proxy so the directory never sees the client IP. Injectable
	/// for tests; production defaults to <see cref="RelayHttpClientFactory"/>.
	/// </summary>
	private readonly Func<System.Uri, IHttpClientFactory> _relayHttpClientFactoryBuilder;
	private readonly TransactionBroadcaster _transactionBroadcaster;

	/// <summary>Serializes coin selection so concurrent sessions cannot reserve the same UTXO.</summary>
	private readonly Lock _coinSelectionLock = new();

	private readonly ConcurrentDictionary<string, PayjoinSessionState> _sessionStates = new();
	private readonly ConcurrentDictionary<string, DateTimeOffset> _relayCooldowns = new();
	private (OhttpKeys Keys, DateTimeOffset FetchedAt)? _cachedOhttpKeys;
	private readonly SemaphoreSlim _ohttpKeysSemaphore = new(1, 1);

	public PayjoinSessionStore SessionStore { get; }

	public PayjoinSessionState? TryGetSessionState(string sessionId) =>
		_sessionStates.TryGetValue(sessionId, out var state) ? state : null;

	/// <summary>
	/// Creates a new receiver session for the given address and returns its initial state,
	/// whose <see cref="PayjoinSessionState.PjUri"/> is the BIP 21 URI to hand to the sender.
	/// </summary>
	public async Task<PayjoinSessionState> StartReceiverSessionAsync(Wallet wallet, string address, CancellationToken cancellationToken)
	{
		OhttpKeys ohttpKeys = await GetOhttpKeysAsync(cancellationToken).ConfigureAwait(false);

		string sessionId = SessionStore.CreateSession(wallet.WalletName, address);
		var persister = new SqliteReceiverSessionPersister(SessionStore, sessionId);

		using var initialBuilder = new ReceiverBuilder(address, _configuration.DirectoryUrl, ohttpKeys);
		using ReceiverBuilder builder = initialBuilder.WithMaxFeeRate(_configuration.MaxFeeRateSatPerVb);
		using InitialReceiveTransition transition = builder.Build();
		using Initialized initialized = transition.Save(persister);

		using PjUri pjUri = initialized.PjUri();
		string uri = pjUri.AsString();
		SessionStore.SetPjUri(sessionId, uri);

		var state = new PayjoinSessionState(sessionId, wallet.WalletName, address, uri, PayjoinSessionStatus.AwaitingSender);
		PublishState(state);

		Logger.LogInfo($"Started payjoin receiver session {sessionId} for wallet '{wallet.WalletName}'.");
		TriggerRound();
		return state;
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		IReadOnlyList<PayjoinSessionRecord> sessions = SessionStore.GetActiveSessions();
		if (sessions.Count == 0)
		{
			return;
		}

		var wallets = (await _getWalletsAsync().ConfigureAwait(false)).ToArray();

		await Parallel.ForEachAsync(
			sessions,
			new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentSessions, CancellationToken = cancel },
			async (session, ct) =>
			{
				try
				{
					await ProcessSessionAsync(session, wallets, ct).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					// A failing session must not take down the poller or the other sessions.
					Logger.LogWarning($"Payjoin session {session.Id}: {ex}");
				}
			}).ConfigureAwait(false);
	}

	private async Task ProcessSessionAsync(PayjoinSessionRecord session, Wallet[] wallets, CancellationToken cancel)
	{
		if (wallets.FirstOrDefault(x => x.WalletName == session.WalletName) is not { } wallet || !wallet.Loaded)
		{
			// Wallet not loaded (yet); leave the session for a later tick.
			return;
		}

		// Re-assert the UTXO reservation from persisted metadata (in-memory flags reset on restart).
		if (session.ReservedOutpoint is { } reserved)
		{
			ReserveCoin(wallet, reserved);
		}

		var persister = new SqliteReceiverSessionPersister(SessionStore, session.Id);

		ReplayResult replay;
		try
		{
			replay = PayjoinMethods.ReplayReceiverEventLog(persister);
		}
		catch (ReceiverReplayException ex)
		{
			using (ex)
			{
				EndSession(session, wallet, ex.IsExpired() ? PayjoinSessionStatus.Expired : PayjoinSessionStatus.Failed, ex.Message);
			}
			return;
		}

		using (replay)
		{
			using ReceiveSession state = replay.State();
			await DriveSessionAsync(session, wallet, persister, state, cancel).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Advances the typestate chain from wherever the replayed state left off. Each step's
	/// transition is saved (persist-before-act) before the next step runs, so a crash
	/// mid-chain resumes at the last saved state.
	/// </summary>
	private async Task DriveSessionAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, ReceiveSession state, CancellationToken cancel)
	{
		try
		{
			switch (state)
			{
				case ReceiveSession.Initialized s:
					await PollForOriginalAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.UncheckedOriginalPayload s:
					await CheckOriginalAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.MaybeInputsOwned s:
					await CheckInputsNotOwnedAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.MaybeInputsSeen s:
					await CheckInputsNotSeenAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.OutputsUnknown s:
					await IdentifyOutputsAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.WantsOutputs s:
					await CommitOutputsAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.WantsInputs s:
					await ContributeInputsAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.WantsFeeRange s:
					await ApplyFeesAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.ProvisionalProposal s:
					await FinalizeProposalAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.PayjoinProposal s:
					await PostProposalAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.Monitor s:
					MonitorPayment(session, wallet, persister, s.Inner);
					break;
				case ReceiveSession.HasReplyableError s:
					await ReplyWithErrorAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.ReceiverPendingFallback s:
					await HandlePendingFallbackAsync(session, wallet, persister, s.Inner, cancel).ConfigureAwait(false);
					break;
				case ReceiveSession.Closed:
					HandleClosed(session, wallet);
					break;
				default:
					Logger.LogWarning($"Payjoin session {session.Id}: unknown replayed state {state.GetType().Name}.");
					break;
			}
		}
		catch (ReceiverPersistedException ex)
		{
			using (ex)
			{
				switch (ex)
				{
					case ReceiverPersistedException.Transient:
						// E.g. transport-level trouble already recorded by the ffi; retry next tick.
						Logger.LogDebug($"Payjoin session {session.Id}: transient error, will retry. {ex.Message}");
						break;
					case ReceiverPersistedException.Fatal:
						// The failure event is persisted; the replay on the next tick yields
						// HasReplyableError (or Closed) and the error reply is posted then.
						Logger.LogInfo($"Payjoin session {session.Id}: proposal rejected. {ex.Message}");
						PublishState(SessionStateFor(session, PayjoinSessionStatus.ProcessingProposal, ex.Message));
						TriggerRound();
						break;
					default:
						Logger.LogError($"Payjoin session {session.Id}: storage error. {ex.Message}");
						break;
				}
			}
		}
	}

	private async Task PollForOriginalAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, Initialized initialized, CancellationToken cancel)
	{
		InitializedTransitionOutcome outcome;
		try
		{
			string relay = PickRelay();
			// The OHTTP context (ClientResponse) is owned by the RequestResponse record and is
			// destroyed with it, so the response must be processed inside this using scope.
			using RequestResponse request = initialized.CreatePollRequest(relay);
			byte[]? body = await PostHttpAsync(session.Id, relay, request.Request, PollRequestTimeout, cancel).ConfigureAwait(false);

			if (body is null)
			{
				// Long-poll returned nothing in time: stasis, not an error (BTCPay plugin issue #17).
				Logger.LogDebug($"Payjoin session {session.Id}: no original proposal yet.");
				PublishState(SessionStateFor(session, PayjoinSessionStatus.AwaitingSender));
				return;
			}

			using InitializedTransition transition = initialized.ProcessResponse(body, request.ClientResponse);
			outcome = transition.Save(persister);
		}
		catch (ReceiverCreateRequestException ex)
		{
			using (ex)
			{
				if (ex.IsExpired())
				{
					EndSession(session, wallet, PayjoinSessionStatus.Expired);
					return;
				}
				throw new InvalidOperationException(ex.Message);
			}
		}

		using InitializedTransitionOutcome outcomeDisposal = outcome;
		if (outcome is InitializedTransitionOutcome.Progress progress)
		{
			Logger.LogInfo($"Payjoin session {session.Id}: original proposal received.");
			PublishState(SessionStateFor(session, PayjoinSessionStatus.ProcessingProposal));
			await CheckOriginalAsync(session, wallet, persister, progress.Inner, cancel).ConfigureAwait(false);
		}
		else
		{
			Logger.LogDebug($"Payjoin session {session.Id}: still awaiting sender (stasis).");
			PublishState(SessionStateFor(session, PayjoinSessionStatus.AwaitingSender));
		}
	}

	private async Task CheckOriginalAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, UncheckedOriginalPayload payload, CancellationToken cancel)
	{
		// Min fee rate: 250 sat/kwu = 1 sat/vb, the mempool minimum; we cannot
		// testmempoolaccept without a node, so the sanity checker covers the rest.
		using UncheckedOriginalPayloadTransition transition = payload.CheckBroadcastSuitability(250, new PayjoinWalletCallbacks.TransactionSanityChecker(_network));
		MaybeInputsOwned next = transition.Save(persister);
		using (next)
		{
			await CheckInputsNotOwnedAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task CheckInputsNotOwnedAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, MaybeInputsOwned state, CancellationToken cancel)
	{
		using MaybeInputsOwnedTransition transition = state.CheckInputsNotOwned(new PayjoinWalletCallbacks.ScriptOwnershipChecker(wallet.KeyManager));
		MaybeInputsSeen next = transition.Save(persister);
		using (next)
		{
			await CheckInputsNotSeenAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task CheckInputsNotSeenAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, MaybeInputsSeen state, CancellationToken cancel)
	{
		using MaybeInputsSeenTransition transition = state.CheckNoInputsSeenBefore(new PayjoinWalletCallbacks.InputsSeenChecker(SessionStore));
		OutputsUnknown next = transition.Save(persister);
		using (next)
		{
			await IdentifyOutputsAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task IdentifyOutputsAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, OutputsUnknown state, CancellationToken cancel)
	{
		using OutputsUnknownTransition transition = state.IdentifyReceiverOutputs(new PayjoinWalletCallbacks.ScriptOwnershipChecker(wallet.KeyManager));
		WantsOutputs next = transition.Save(persister);
		using (next)
		{
			await CommitOutputsAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task CommitOutputsAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, WantsOutputs state, CancellationToken cancel)
	{
		// No output substitution: the payment output stays on the announced address.
		using WantsOutputsTransition transition = state.CommitOutputs();
		WantsInputs next = transition.Save(persister);
		using (next)
		{
			await ContributeInputsAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task ContributeInputsAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, WantsInputs state, CancellationToken cancel)
	{
		WantsFeeRange next;

		// The lock spans candidate listing through reservation, so two sessions cannot pick
		// the same coin (concurrent payjoins reusing one input).
		lock (_coinSelectionLock)
		{
			SmartCoin[] candidates = wallet.Coins
				.Where(x => x.Confirmed && x.IsAvailable() && !x.IsBanned)
				.ToArray();

			if (candidates.Length == 0)
			{
				throw new InvalidOperationException("No spendable coins to contribute; leaving the proposal input-less is not supported.");
			}

			// The library's UIH heuristic picks the privacy-preserving single input.
			using InputPair selected = state.TryPreservingPrivacy(candidates.Select(PayjoinWalletCallbacks.ToInputPair).ToArray());
			OutPoint selectedOutpoint = new(uint256.Parse(selected.Outpoint().Txid), selected.Outpoint().Vout);
			SmartCoin selectedCoin = candidates.Single(x => x.Outpoint == selectedOutpoint);

			// Reserve before committing: a crash between the two leaves a stale reservation
			// (released at session end) rather than a double-spendable contribution.
			selectedCoin.PayjoinInProgress = true;
			SessionStore.SetReservedOutpoint(session.Id, selectedOutpoint);

			using WantsInputs contributed = state.ContributeInputs([selected]);
			using WantsInputsTransition transition = contributed.CommitInputs();
			next = transition.Save(persister);
		}

		using (next)
		{
			await ApplyFeesAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task ApplyFeesAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, WantsFeeRange state, CancellationToken cancel)
	{
		using WantsFeeRangeTransition transition = state.ApplyFeeRange(null, _configuration.MaxFeeRateSatPerVb);
		ProvisionalProposal next = transition.Save(persister);
		using (next)
		{
			await FinalizeProposalAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task FinalizeProposalAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, ProvisionalProposal state, CancellationToken cancel)
	{
		if (wallet.KeyManager.IsWatchOnly)
		{
			throw new InvalidOperationException("Watch-only wallets cannot sign payjoin contributions.");
		}

		if (!wallet.IsLoggedIn)
		{
			// Hot signing needs the session password; wait until the user logs in.
			Logger.LogDebug($"Payjoin session {session.Id}: waiting for wallet login to sign the proposal.");
			return;
		}

		// The reserved coin is the contributed input recorded at the WantsInputs step.
		SmartCoin[] contributedCoins = SessionStore.TryGetSession(session.Id)?.ReservedOutpoint is { } outpoint
			? wallet.Coins.Where(x => x.Outpoint == outpoint).ToArray()
			: [];

		var signer = new PayjoinWalletCallbacks.ContributedInputSigner(_network, wallet.KeyManager, wallet.Password, contributedCoins);
		using ProvisionalProposalTransition transition = state.FinalizeProposal(signer);
		PayjoinProposal next = transition.Save(persister);
		using (next)
		{
			await PostProposalAsync(session, wallet, persister, next, cancel).ConfigureAwait(false);
		}
	}

	private async Task PostProposalAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, PayjoinProposal proposal, CancellationToken cancel)
	{
		// Record the expected txid before posting: settlement detection and UI status depend
		// on it, and the txid of a fully segwit transaction is stable under signing.
		PSBT proposalPsbt = PSBT.Parse(proposal.Psbt(), _network);
		SessionStore.SetProposalTxid(session.Id, proposalPsbt.GetGlobalTransaction().GetHash());

		string relay = PickRelay();
		using RequestResponse request = proposal.CreatePostRequest(relay);
		byte[]? body = await PostHttpAsync(session.Id, relay, request.Request, PollRequestTimeout * 3, cancel).ConfigureAwait(false);
		if (body is null)
		{
			// Transport failure; the proposal was not delivered. Stay in PayjoinProposal and retry.
			return;
		}

		using PayjoinProposalTransition transition = proposal.ProcessResponse(body, request.ClientResponse);
		using Monitor monitor = transition.Save(persister);

		Logger.LogInfo($"Payjoin session {session.Id}: proposal posted to the directory.");
		PublishState(SessionStateFor(session, PayjoinSessionStatus.ProposalSent));
		MonitorPayment(session, wallet, persister, monitor);
	}

	private void MonitorPayment(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, Monitor monitor)
	{
		using MonitorTransition transition = monitor.CheckForTransaction(new PayjoinWalletCallbacks.WalletTransactionFinder(wallet.TransactionStore));
		transition.Save(persister);

		if (IsProposalInWallet(session, wallet))
		{
			EndSession(session, wallet, PayjoinSessionStatus.Completed);
		}
		else
		{
			PublishState(SessionStateFor(session, PayjoinSessionStatus.ProposalSent));
		}
	}

	private async Task ReplyWithErrorAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, HasReplyableError error, CancellationToken cancel)
	{
		string relay = PickRelay();
		using RequestResponse request = error.CreateErrorRequest(relay);
		byte[]? body = await PostHttpAsync(session.Id, relay, request.Request, PollRequestTimeout * 3, cancel).ConfigureAwait(false);
		if (body is null)
		{
			// Could not deliver the error reply; retry next tick so the sender learns the failure.
			return;
		}

		using HasReplyableErrorTransition transition = error.ProcessErrorResponse(body, request.ClientResponse);
		ReceiverPendingFallback? pendingFallback = transition.Save(persister);

		Logger.LogInfo($"Payjoin session {session.Id}: error reply delivered to the sender.");
		if (pendingFallback is not null)
		{
			using (pendingFallback)
			{
				await HandlePendingFallbackAsync(session, wallet, persister, pendingFallback, cancel).ConfigureAwait(false);
			}
		}
		else
		{
			EndSession(session, wallet, PayjoinSessionStatus.Failed);
		}
	}

	private async Task HandlePendingFallbackAsync(PayjoinSessionRecord session, Wallet wallet, SqliteReceiverSessionPersister persister, ReceiverPendingFallback pendingFallback, CancellationToken cancel)
	{
		// The payjoin definitively failed but the sender's signed original still pays us:
		// broadcast it. The sender may broadcast the same transaction; same txid, no conflict.
		byte[] fallbackBytes = pendingFallback.FallbackTx();
		try
		{
			var fallbackTx = new SmartTransaction(Transaction.Load(fallbackBytes, _network), Models.Height.Unknown);
			await _transactionBroadcaster.SendTransactionAsync(fallbackTx, cancel).ConfigureAwait(false);
			Logger.LogInfo($"Payjoin session {session.Id}: fallback transaction {fallbackTx.GetHash()} broadcast.");
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Payjoin session {session.Id}: fallback broadcast failed; the sender can still broadcast it. {ex.Message}");
		}

		using PendingFallbackTransition transition = pendingFallback.Close();
		transition.Save(persister);
		EndSession(session, wallet, PayjoinSessionStatus.Failed, "Payjoin failed; fell back to the original transaction.");
	}

	private void HandleClosed(PayjoinSessionRecord session, Wallet wallet)
	{
		EndSession(session, wallet, IsProposalInWallet(session, wallet) ? PayjoinSessionStatus.Completed : PayjoinSessionStatus.Failed);
	}

	private bool IsProposalInWallet(PayjoinSessionRecord session, Wallet wallet) =>
		SessionStore.TryGetSession(session.Id)?.ProposalTxid is { } txid && wallet.TransactionStore.TryGetTransaction(txid, out _);

	/// <summary>Closes the session row, releases the coin reservation, and publishes the final status.</summary>
	private void EndSession(PayjoinSessionRecord session, Wallet wallet, PayjoinSessionStatus status, string? errorMessage = null)
	{
		// Release the reservation; on completion the coin is spent by the payjoin
		// transaction anyway, so clearing the flag is always correct. The all-coins view is
		// required: a completed session's coin is already spent and out of the unspent
		// registry, and a stale flag would keep it reserved forever if a reorg returned it.
		if (SessionStore.TryGetSession(session.Id)?.ReservedOutpoint is { } outpoint
			&& wallet.GetAllCoins().FirstOrDefault(x => x.Outpoint == outpoint) is { } coin)
		{
			coin.PayjoinInProgress = false;
		}

		SessionStore.CloseSession(session.Id);
		PublishState(SessionStateFor(session, status, errorMessage));
		Logger.LogInfo($"Payjoin session {session.Id} ended: {status}.");
	}

	private void ReserveCoin(Wallet wallet, OutPoint outpoint)
	{
		if (wallet.Coins.FirstOrDefault(x => x.Outpoint == outpoint) is { } coin && !coin.PayjoinInProgress)
		{
			coin.PayjoinInProgress = true;
		}
	}

	private PayjoinSessionState SessionStateFor(PayjoinSessionRecord session, PayjoinSessionStatus status, string? errorMessage = null) =>
		new(session.Id, session.WalletName, session.Address, session.PjUri ?? SessionStore.TryGetSession(session.Id)?.PjUri, status, errorMessage);

	private void PublishState(PayjoinSessionState state)
	{
		_sessionStates[state.SessionId] = state;
		SessionStatusChanged?.Invoke(this, state);
	}

	/// <summary>
	/// Posts an OHTTP-encapsulated request through Wasabi's HTTP client factory (per-session
	/// Tor stream isolation). Returns <c>null</c> on transport failure after quarantining the
	/// relay, so callers treat it as "retry later" rather than a session error.
	/// </summary>
	private async Task<byte[]?> PostHttpAsync(string sessionId, string relay, Request request, TimeSpan timeout, CancellationToken cancel)
	{
		try
		{
			HttpClient client = _httpClientFactory.CreateClient($"payjoin-session-{sessionId}");
			using var content = new ByteArrayContent(request.Body);
			content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
			timeoutCts.CancelAfter(timeout);

			using HttpResponseMessage response = await client.PostAsync(request.Url, content, timeoutCts.Token).ConfigureAwait(false);
			return await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancel.IsCancellationRequested))
		{
			_relayCooldowns[relay] = DateTimeOffset.UtcNow + RelayCooldown;
			Logger.LogDebug($"Payjoin session {sessionId}: relay {relay} unavailable ({ex.GetType().Name}); quarantined.");
			return null;
		}
	}

	/// <summary>Random relay choice among those not in cooldown; falls back to any configured relay.</summary>
	private string PickRelay()
	{
		string[] healthy = _configuration.OhttpRelayUrls
			.Where(x => !_relayCooldowns.TryGetValue(x, out var until) || until < DateTimeOffset.UtcNow)
			.ToArray();
		string[] pool = healthy.Length > 0 ? healthy : _configuration.OhttpRelayUrls;

		return pool[Random.Shared.Next(pool.Length)];
	}

	/// <summary>
	/// Fetches the directory's OHTTP keys so the client IP is never revealed to the
	/// directory: through Tor when it is enabled (the whole HTTP factory rides Tor), else
	/// through a relay acting as a CONNECT proxy. Cached for 12 hours.
	/// </summary>
	/// <remarks>Internal for tests.</remarks>
	internal async Task<OhttpKeys> GetOhttpKeysAsync(CancellationToken cancellationToken)
	{
		await _ohttpKeysSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_cachedOhttpKeys is { } cached && cached.FetchedAt + OhttpKeysCacheDuration > DateTimeOffset.UtcNow)
			{
				return cached.Keys;
			}

			OhttpKeys keys = _configuration.TorEnabled
				? await FetchOhttpKeysViaTorAsync(cancellationToken).ConfigureAwait(false)
				: await FetchOhttpKeysViaRelaysAsync(cancellationToken).ConfigureAwait(false);
			_cachedOhttpKeys = (keys, DateTimeOffset.UtcNow);
			return keys;
		}
		finally
		{
			_ohttpKeysSemaphore.Release();
		}
	}

	/// <summary>Tor path: a straight GET to the directory's well-known endpoint over the (Tor-riding) factory client.</summary>
	private async Task<OhttpKeys> FetchOhttpKeysViaTorAsync(CancellationToken cancellationToken)
	{
		HttpClient client = _httpClientFactory.CreateClient("payjoin-ohttp-keys-bootstrap");
		var keysUrl = new System.Uri(new System.Uri(_configuration.DirectoryUrl), "/.well-known/ohttp-gateway");
		using var request = new HttpRequestMessage(HttpMethod.Get, keysUrl);
		request.Headers.Accept.ParseAdd("application/ohttp-keys");

		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		return OhttpKeys.Decode(bytes);
	}

	/// <summary>Clearnet path: relay-as-CONNECT-proxy bootstrap so the directory never sees the client IP.</summary>
	private async Task<OhttpKeys> FetchOhttpKeysViaRelaysAsync(CancellationToken cancellationToken)
	{
		Exception? lastError = null;
		foreach (string relay in _configuration.OhttpRelayUrls.OrderBy(_ => Random.Shared.Next()))
		{
			try
			{
				return await FetchOhttpKeysViaRelayAsync(relay, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex)
			{
				lastError = ex;
				Logger.LogDebug($"OHTTP keys fetch via relay {relay} failed: {ex.Message}");
			}
		}

		throw new HttpRequestException($"Could not fetch OHTTP keys from {_configuration.DirectoryUrl} via any configured relay.", lastError);
	}

	/// <summary>
	/// Fetches the directory's well-known OHTTP keys through the relay acting as a CONNECT
	/// proxy — riding Wasabi's own HTTP factory (relay set as proxy), not the payjoin-ffi's
	/// transport, so the ffi is reduced to the pure <see cref="OhttpKeys.Decode"/> parser and
	/// every byte still flows through Wasabi's handler stack.
	/// </summary>
	private async Task<OhttpKeys> FetchOhttpKeysViaRelayAsync(string relay, CancellationToken cancellationToken)
	{
		IHttpClientFactory relayFactory = _relayHttpClientFactoryBuilder(new System.Uri(relay));
		HttpClient client = relayFactory.CreateClient("payjoin-ohttp-keys-bootstrap");
		var keysUrl = new System.Uri(new System.Uri(_configuration.DirectoryUrl), "/.well-known/ohttp-gateway");
		using var request = new HttpRequestMessage(HttpMethod.Get, keysUrl);
		request.Headers.Accept.ParseAdd("application/ohttp-keys");

		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		return OhttpKeys.Decode(bytes);
	}

	public override void Dispose()
	{
		SessionStore.Dispose();
		_ohttpKeysSemaphore.Dispose();
		base.Dispose();
	}
}
