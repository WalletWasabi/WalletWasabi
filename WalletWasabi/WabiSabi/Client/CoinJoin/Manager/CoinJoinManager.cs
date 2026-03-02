using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.Wallets;
using static WalletWasabi.Logging.LoggerTools;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinManager : BackgroundService
{
	public CoinJoinManager(
		IWalletProvider walletProvider,
		RoundStateProvider roundStatusProvider,
		Func<string, IWabiSabiApiRequestHandler> arenaRequestHandlerFactory,
		CoinJoinConfiguration coinJoinConfiguration,
		CoinPrison coinPrison,
		EventBus eventBus)
	{
		_walletProvider = walletProvider;
		ArenaRequestHandlerFactory = arenaRequestHandlerFactory;
		_roundStatusProvider = roundStatusProvider;
		_coinJoinConfiguration = coinJoinConfiguration;
		_coinPrison = coinPrison;
		_serverTipHeightChangeSubscription = eventBus.Subscribe<ServerTipHeightChanged>(h => _serverTipHeight = h.Height);
	}

	public event EventHandler<StatusChangedEventArgs>? StatusChanged;

	public ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> CoinsInCriticalPhase { get; set; } = ImmutableDictionary<WalletId, ImmutableList<SmartCoin>>.Empty;
	private readonly IWalletProvider _walletProvider;
	private Func<string, IWabiSabiApiRequestHandler> ArenaRequestHandlerFactory { get; }
	private readonly RoundStateProvider _roundStatusProvider;
	private readonly CoinPrison _coinPrison;
	private readonly CoinRefrigerator _coinRefrigerator = new();
	private readonly CoinJoinConfiguration _coinJoinConfiguration;
	private uint _serverTipHeight;

	public CoinJoinClientState HighestCoinJoinClientState => CoinJoinClientStates.Values.Any()
		? CoinJoinClientStates.Values.Select(x => x.CoinJoinClientState).MaxBy(s => (int)s)
		: CoinJoinClientState.Idle;

	private ImmutableDictionary<WalletId, CoinJoinClientStateHolder> CoinJoinClientStates { get; set; } = ImmutableDictionary<WalletId, CoinJoinClientStateHolder>.Empty;

	private readonly IDisposable _serverTipHeightChangeSubscription;

	private MailboxProcessor<CoinJoinMessage>? _mailboxProcessor;

	private static bool IsUnderPlebStop(SmartCoin[] coinCandidates, Money plebStopThreshold) => coinCandidates.Sum(x => x.Amount) < plebStopThreshold;

	#region Public API (Start | Stop | TryGetWalletStatus)

	public async Task StartAsync(IWallet wallet, IWallet outputWallet, bool stopWhenAllMixed, bool overridePlebStop, CancellationToken cancellationToken)
	{
		var coinCandidates = (await GetCoinSelectionAsync(wallet).ConfigureAwait(false)).CandidateCoins;

		if (overridePlebStop && !IsUnderPlebStop(coinCandidates, wallet.PlebStopThreshold))
		{
			// Turn off overriding if we reached or exceeded the threshold meanwhile.
			overridePlebStop = false;
			Logger.LogDebug("Do not override PlebStop anymore, confirmed balance no longer below the threshold.", wallet);
		}

		// Use MailboxProcessor instead of old channel
		_mailboxProcessor?.Post(new StartCoinJoin(wallet, outputWallet, stopWhenAllMixed, overridePlebStop));
	}

	public async Task StopAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		// Use MailboxProcessor instead of old channel
		_mailboxProcessor?.Post(new StopCoinJoin(wallet));
	}

	public async Task<CoinJoinClientState> GetCoinjoinClientStateAsync(WalletId walletId, CancellationToken cancellationToken = default)
	{
		if (_mailboxProcessor is null)
		{
			// Fallback to old logic during transition
			if (CoinJoinClientStates.TryGetValue(walletId, out var coinJoinClientStateHolder))
			{
				return coinJoinClientStateHolder.CoinJoinClientState;
			}
			throw new ArgumentException($"Wallet {walletId} is not tracked.");
		}

		return await _mailboxProcessor.PostAndReplyAsync<CoinJoinClientState>(
			replyChannel => new GetCoinJoinState(walletId, replyChannel),
			cancellationToken).ConfigureAwait(false);
	}

	#endregion Public API (Start | Stop | TryGetWalletStatus)

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Start MailboxProcessor-based architecture
		StartMailboxProcessor(stoppingToken);

		// Start periodic timers - these will post messages to the mailbox
		var walletMonitorTask = RunWalletMonitorTimerAsync(stoppingToken);
		var finalizationTask = RunFinalizationTimerAsync(stoppingToken);
		var restartCheckTask = RunRestartCheckTimerAsync(stoppingToken);

		// Wait for completion
		await Task.WhenAll(walletMonitorTask, finalizationTask, restartCheckTask).ConfigureAwait(false);
	}

	#region New MailboxProcessor Architecture

	private void StartMailboxProcessor(CancellationToken stoppingToken)
	{
		_mailboxProcessor = new MailboxProcessor<CoinJoinMessage>(
			async (mailbox, ct) =>
			{
				var state = ManagerState.Empty;
				while (!ct.IsCancellationRequested)
				{
					try
					{
						var msg = await mailbox.ReceiveAsync(ct).ConfigureAwait(false);
						state = await HandleMessageAsync(msg, state, ct).ConfigureAwait(false);

						// Update public properties from state
							CoinJoinClientStates = state.CoinJoinClientStates;
						CoinsInCriticalPhase = state.CoinsInCriticalPhase;
					}
					catch (OperationCanceledException) when (ct.IsCancellationRequested)
					{
						// Normal cancellation
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error handling CoinJoinMessage: {ex}");
					}
				}
			},
			stoppingToken);

		_mailboxProcessor.Start();

		// Start periodic timers - these will post messages to the mailbox
		_ = Task.Run(() => RunWalletMonitorTimerAsync(stoppingToken), stoppingToken);
		_ = Task.Run(() => RunFinalizationTimerAsync(stoppingToken), stoppingToken);
		_ = Task.Run(() => RunRestartCheckTimerAsync(stoppingToken), stoppingToken);
	}

	private async Task RunWalletMonitorTimerAsync(CancellationToken ct)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
			while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
			{
				_mailboxProcessor?.Post(new UpdateWalletStates());
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Normal cancellation
		}
	}

	private async Task RunFinalizationTimerAsync(CancellationToken ct)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
			while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
			{
				_mailboxProcessor?.Post(new CheckFinalization());
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Normal cancellation
		}
	}

	private async Task RunRestartCheckTimerAsync(CancellationToken ct)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
			while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
			{
				_mailboxProcessor?.Post(new CheckScheduledRestarts());
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Normal cancellation
		}
	}

	private async Task<ManagerState> HandleMessageAsync(
		CoinJoinMessage msg,
		ManagerState state,
		CancellationToken cancellationToken)
	{
		return msg switch
		{
			StartCoinJoin start => await HandleStartCoinJoinAsync(start, state, cancellationToken).ConfigureAwait(false),
			StopCoinJoin stop => HandleStopCoinJoin(stop, state),
			UpdateWalletStates => await HandleUpdateWalletStatesAsync(state).ConfigureAwait(false),
			CheckFinalization => await HandleCheckFinalizationAsync(state, cancellationToken).ConfigureAwait(false),
			CheckScheduledRestarts => HandleCheckScheduledRestarts(state),
			WalletEnteredSendWorkflowMsg entered => HandleWalletEnteredSendWorkflow(entered, state),
			WalletLeftSendWorkflowMsg left => HandleWalletLeftSendWorkflow(left, state),
			WalletEnteredSendingMsg entering => await HandleWalletEnteredSendingAsync(entering, state).ConfigureAwait(false),
			SignalStopAllCoinjoins => await HandleSignalStopAllCoinjoinsAsync(state, cancellationToken).ConfigureAwait(false),
			RestartAbortedCoinjoins => HandleRestartAbortedCoinjoins(state),
			GetCoinJoinState query => HandleGetCoinJoinStateQuery(query, state),
			_ => state
		};
	}

	private async Task<ManagerState> HandleStartCoinJoinAsync(StartCoinJoin msg, ManagerState state, CancellationToken ct)
	{
		var wallet = msg.Wallet;
		var outputWallet = msg.OutputWallet;
		var stopWhenAllMixed = msg.StopWhenAllMixed;
		var overridePlebStop = msg.OverridePlebStop;

		// Check if already running
		if (state.TrackedCoinJoins.TryGetValue(wallet.WalletId, out var existingTracker))
		{
			if (stopWhenAllMixed != existingTracker.StopWhenAllMixed)
			{
				existingTracker.StopWhenAllMixed = stopWhenAllMixed;
				Logger.LogDebug(FormatLog($"Cannot start coinjoin, because it is already running - but updated the value of {nameof(stopWhenAllMixed)} to {stopWhenAllMixed}.", wallet));
			}
			else
			{
				Logger.LogDebug(FormatLog("Cannot start coinjoin, because it is already running.", wallet));
			}

			// On cancelling the shutdown prevention, we need to set it back to false
			existingTracker.IsStopped = false;
			return state;
		}

		// Sanity checks and coin selection
		async Task<IEnumerable<SmartCoin>> SanityChecksAndGetCoinCandidatesFunc()
		{
			if (state.WalletsBlockedByUi.ContainsKey(wallet.WalletId))
			{
				throw new CoinJoinClientException(CoinjoinError.UserInSendWorkflow);
			}

			var coinSelectionResult = await SelectCandidateCoinsAsync(wallet).ConfigureAwait(false);
			var coinCandidates = coinSelectionResult.CandidateCoins;

			if (IsUnderPlebStop(coinCandidates, wallet.PlebStopThreshold) && !overridePlebStop)
			{
				Logger.LogTrace(FormatLog("PlebStop preventing coinjoin.", wallet));

				if (!IsUnderPlebStop(coinCandidates.Union(coinSelectionResult.UnconfirmedCoins).ToArray(), wallet.PlebStopThreshold))
				{
					throw new CoinJoinClientException(CoinjoinError.NotEnoughConfirmedUnprivateBalance);
				}

				throw new CoinJoinClientException(CoinjoinError.NotEnoughUnprivateBalance);
			}

			// If there are pending payments, ignore already achieved privacy
			if (!wallet.BatchedPayments.AreTherePendingPayments)
			{
				// If all coins are already private, then don't mix
				if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
				{
					Logger.LogTrace(FormatLog("All mixed!", wallet));
					throw new CoinJoinClientException(CoinjoinError.AllCoinsPrivate);
				}

				// If all coin candidates are private it makes no sense to mix
				if (coinCandidates.All(x => x.IsPrivate(wallet.AnonScoreTarget)))
				{
					throw new CoinJoinClientException(
						CoinjoinError.NoCoinsEligibleToMix,
						$"All coin candidates are already private and {nameof(stopWhenAllMixed)} was {stopWhenAllMixed}");
				}
			}

			NotifyWalletStartedCoinJoin(wallet);

			return coinCandidates;
		}

		// Create tracker
		var coinJoinTrackerFactory = new CoinJoinTrackerFactory(ArenaRequestHandlerFactory, _roundStatusProvider, _coinJoinConfiguration, ct);
		var coinJoinTracker = await coinJoinTrackerFactory.CreateAndStartAsync(
			wallet,
			outputWallet,
			SanityChecksAndGetCoinCandidatesFunc,
			stopWhenAllMixed,
			overridePlebStop).ConfigureAwait(false);

		// Subscribe to progress events
		coinJoinTracker.WalletCoinJoinProgressChanged += CoinJoinTracker_WalletCoinJoinProgressChanged;

		var registrationTimeout = TimeSpan.MaxValue;
		NotifyCoinJoinStarted(wallet, registrationTimeout);

		Logger.LogDebug(FormatLog($"{nameof(CoinJoinClient)} started.", wallet));
		Logger.LogDebug(FormatLog($"{nameof(stopWhenAllMixed)}:'{stopWhenAllMixed}' {nameof(overridePlebStop)}:'{overridePlebStop}'.", wallet));

		// Update state: add tracker, remove scheduled restart if exists
		return state with
		{
			TrackedCoinJoins = state.TrackedCoinJoins.SetItem(wallet.WalletId, coinJoinTracker),
			ScheduledRestarts = state.ScheduledRestarts.Remove(wallet.WalletId)
		};
	}

	private ManagerState HandleStopCoinJoin(StopCoinJoin msg, ManagerState state)
	{
		var wallet = msg.Wallet;
		var updatedScheduledRestarts = state.ScheduledRestarts;
		var scheduledRestartRemoved = false;

		// Remove scheduled restart if exists
		if (state.ScheduledRestarts.ContainsKey(wallet.WalletId))
		{
			updatedScheduledRestarts = updatedScheduledRestarts.Remove(wallet.WalletId);
			scheduledRestartRemoved = true;
		}

		// Stop active tracker if exists
		if (state.TrackedCoinJoins.TryGetValue(wallet.WalletId, out var tracker))
		{
			tracker.Stop();
			if (tracker.InCriticalCoinJoinState)
			{
				Logger.LogWarning(FormatLog("Coinjoin is in critical phase, it cannot be stopped - it won't restart later.", wallet));
			}
		}
		else if (scheduledRestartRemoved)
		{
			// Only notify if we removed a scheduled restart but there was no active tracker
			NotifyWalletStoppedCoinJoin(wallet);
		}

		return state with { ScheduledRestarts = updatedScheduledRestarts };
	}

	private async Task<ManagerState> HandleUpdateWalletStatesAsync(ManagerState state)
	{
		var mixableWallets = await GetMixableWalletsAsync().ConfigureAwait(false);

		// Identify newly opened wallets (not in TrackedWallets)
		var openedWallets = mixableWallets.Where(x => !state.TrackedWallets.ContainsKey(x.Key)).ToImmutableList();
		foreach (var openedWallet in openedWallets.Select(x => x.Value))
		{
			NotifyMixableWalletLoaded(openedWallet);
		}

		// Identify newly closed wallets (in TrackedWallets but not mixable)
		var closedWallets = state.TrackedWallets.Where(x => !mixableWallets.ContainsKey(x.Key)).ToImmutableList();
		foreach (var closedWallet in closedWallets.Select(x => x.Value))
		{
			NotifyMixableWalletUnloaded(closedWallet);
		}

		// Update TrackedWallets immutably
		return state with { TrackedWallets = mixableWallets };
	}

	private async Task<ManagerState> HandleCheckFinalizationAsync(ManagerState state, CancellationToken ct)
	{
		// Find completed coinjoins
		var finishedCoinJoins = state.TrackedCoinJoins
			.Where(x => x.Value.IsCompleted)
			.Select(x => x.Value)
			.ToImmutableArray();

		var updatedTrackedCoinJoins = state.TrackedCoinJoins;
		var updatedScheduledRestarts = state.ScheduledRestarts;

		// Handle each finished coinjoin
		foreach (var finishedCoinJoin in finishedCoinJoins)
		{
			var wallet = finishedCoinJoin.Wallet;
			var shouldScheduleRestart = await HandleFinalizationLogicAsync(finishedCoinJoin, ct).ConfigureAwait(false);

			// Remove from tracked coinjoins
			updatedTrackedCoinJoins = updatedTrackedCoinJoins.Remove(wallet.WalletId);

			// Schedule restart if needed
			if (shouldScheduleRestart)
			{
				var scheduledFor = DateTimeOffset.UtcNow.AddSeconds(30);
				var scheduledRestart = new ScheduledRestart(
					wallet,
					finishedCoinJoin.OutputWallet,
					finishedCoinJoin.StopWhenAllMixed,
					finishedCoinJoin.OverridePlebStop,
					scheduledFor,
					Guid.NewGuid());

				updatedScheduledRestarts = updatedScheduledRestarts.SetItem(wallet.WalletId, scheduledRestart);
			}

			// Cleanup tracker
			finishedCoinJoin.WalletCoinJoinProgressChanged -= CoinJoinTracker_WalletCoinJoinProgressChanged;
			finishedCoinJoin.Dispose();
		}

		// Update CoinJoinClientStates and CoinsInCriticalPhase
		var wallets = await _walletProvider.GetWalletsAsync().ConfigureAwait(false);
		var updatedCoinJoinClientStates = BuildCoinJoinClientStates(wallets, updatedTrackedCoinJoins, updatedScheduledRestarts);
		var updatedCoinsInCriticalPhase = BuildCoinsInCriticalPhase(wallets, updatedTrackedCoinJoins);

		return state with
		{
			TrackedCoinJoins = updatedTrackedCoinJoins,
			ScheduledRestarts = updatedScheduledRestarts,
			CoinJoinClientStates = updatedCoinJoinClientStates,
			CoinsInCriticalPhase = updatedCoinsInCriticalPhase
		};
	}

	// Returns true if should schedule restart
	private async Task<bool> HandleFinalizationLogicAsync(CoinJoinTracker finishedCoinJoin, CancellationToken ct)
	{
		var wallet = finishedCoinJoin.Wallet;
		var destinationProvider = finishedCoinJoin.OutputWallet.OutputProvider.DestinationProvider;
		var batchedPayments = wallet.BatchedPayments;
		CoinJoinClientException? cjClientException = null;
		var forceStop = false;

		try
		{
			var result = await finishedCoinJoin.CoinJoinTask.ConfigureAwait(false);
			if (result is SuccessfulCoinJoinResult successfulCoinjoin)
			{
				_coinRefrigerator.Freeze(successfulCoinjoin.Coins);
				batchedPayments.MovePaymentsToFinished(successfulCoinjoin.UnsignedCoinJoin.GetHash());
				MarkDestinationsUsed(destinationProvider, successfulCoinjoin.OutputScripts);
				Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was broadcast.", wallet));
			}
			else
			{
				Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was not broadcast.", wallet));
			}
		}
		catch (UnknownRoundEndingException ex)
		{
			_coinRefrigerator.Freeze(ex.Coins);
			MarkDestinationsUsed(destinationProvider, ex.OutputScripts);
			Logger.LogDebug(FormatLog(ex.ToString(), wallet));
		}
		catch (CoinJoinClientException clientException)
		{
			cjClientException = clientException;
			if (cjClientException.CoinjoinError is CoinjoinError.CoordinatorLiedAboutInputs)
			{
				Logger.LogError(cjClientException);
				forceStop = true;
			}
			else
			{
				Logger.LogDebug(cjClientException);
			}
		}
		catch (InvalidOperationException ioe)
		{
			Logger.LogWarning(ioe);
		}
		catch (OperationCanceledException)
		{
			if (finishedCoinJoin.IsStopped)
			{
				Logger.LogInfo($"{nameof(CoinJoinClient)} stopped.", wallet);
			}
			else
			{
				Logger.LogInfo($"{nameof(CoinJoinClient)} was cancelled.", wallet);
			}
		}
		catch (UnexpectedRoundPhaseException e)
		{
			Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} failed with exception: '{e}'", wallet));
		}
		catch (WabiSabiProtocolException wpe) when (wpe.ErrorCode == WabiSabiProtocolErrorCode.WrongPhase)
		{
			Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} failed with: '{wpe.Message}'", wallet));
		}
		catch (Exception e)
		{
			Logger.LogError(FormatLog($"{nameof(CoinJoinClient)} failed with exception: '{e}'", wallet));
		}
		finally
		{
			batchedPayments.MovePaymentsToPending();
		}

		// Ban coins if needed
		if (finishedCoinJoin.BannedCoins.Count != 0)
		{
			foreach (var info in finishedCoinJoin.BannedCoins)
			{
				_coinPrison.Ban(info.Coin, info.BanUntilUtc);
			}
		}

		NotifyCoinJoinCompletion(finishedCoinJoin);

		// Decide if should schedule restart
		if (forceStop || finishedCoinJoin.IsStopped || ct.IsCancellationRequested)
		{
			NotifyWalletStoppedCoinJoin(wallet);
			return false; // Don't restart
		}
		else if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
		{
			NotifyCoinJoinStartError(wallet, CoinjoinError.AllCoinsPrivate);
			if (!finishedCoinJoin.StopWhenAllMixed)
			{
				// In auto CJ mode we never stop trying
				return true; // Schedule restart
			}
			else
			{
				// We finished with CJ permanently
				NotifyWalletStoppedCoinJoin(wallet);
				return false;
			}
		}
		else if (cjClientException is not null)
		{
			// Keep trying, so CJ starts automatically when the wallet becomes mixable again
			NotifyCoinJoinStartError(wallet, cjClientException.CoinjoinError);
			return true; // Schedule restart
		}
		else
		{
			Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} restart automatically.", wallet));
			return true; // Schedule restart
		}
	}

	private ImmutableDictionary<WalletId, CoinJoinClientStateHolder> BuildCoinJoinClientStates(
		IEnumerable<IWallet> wallets,
		ImmutableDictionary<WalletId, CoinJoinTracker> trackedCoinJoins,
		ImmutableDictionary<WalletId, ScheduledRestart> scheduledRestarts)
	{
		var coinJoinClientStates = ImmutableDictionary.CreateBuilder<WalletId, CoinJoinClientStateHolder>();

		foreach (var wallet in wallets)
		{
			CoinJoinClientStateHolder state = new(CoinJoinClientState.Idle, StopWhenAllMixed: true, OverridePlebStop: false, OutputWallet: wallet);

			if (trackedCoinJoins.TryGetValue(wallet.WalletId, out var coinJoinTracker) && !coinJoinTracker.IsCompleted)
			{
				var trackerState = coinJoinTracker.InCriticalCoinJoinState
					? CoinJoinClientState.InCriticalPhase
					: CoinJoinClientState.InProgress;

				state = new(trackerState, coinJoinTracker.StopWhenAllMixed, coinJoinTracker.OverridePlebStop, coinJoinTracker.OutputWallet);
			}
			else if (scheduledRestarts.TryGetValue(wallet.WalletId, out var scheduledRestart))
			{
				state = new(CoinJoinClientState.InSchedule, scheduledRestart.StopWhenAllMixed, scheduledRestart.OverridePlebStop, scheduledRestart.OutputWallet);
			}

			coinJoinClientStates.Add(wallet.WalletId, state);
		}

		return coinJoinClientStates.ToImmutable();
	}

	private ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> BuildCoinsInCriticalPhase(
		IEnumerable<IWallet> wallets,
		ImmutableDictionary<WalletId, CoinJoinTracker> trackedCoinJoins)
	{
		var coinsUsedInCoinjoins = ImmutableDictionary.CreateBuilder<WalletId, ImmutableList<SmartCoin>>();

		foreach (var wallet in wallets)
		{
			ImmutableList<SmartCoin> coinsInCoinjoin = ImmutableList<SmartCoin>.Empty;

			if (trackedCoinJoins.TryGetValue(wallet.WalletId, out var coinJoinTracker) && !coinJoinTracker.IsCompleted)
			{
				coinsInCoinjoin = coinJoinTracker.CoinsInCriticalPhase;
			}

			coinsUsedInCoinjoins.Add(wallet.WalletId, coinsInCoinjoin);
		}

		return coinsUsedInCoinjoins.ToImmutable();
	}

	private ManagerState HandleCheckScheduledRestarts(ManagerState state)
	{
		var now = DateTimeOffset.UtcNow;
		var updatedScheduledRestarts = state.ScheduledRestarts;

		foreach (var (walletId, scheduledRestart) in state.ScheduledRestarts)
		{
			if (now >= scheduledRestart.ScheduledFor)
			{
				// Time to restart - post the start message
				_mailboxProcessor?.Post(new StartCoinJoin(
					scheduledRestart.Wallet,
					scheduledRestart.OutputWallet,
					scheduledRestart.StopWhenAllMixed,
					scheduledRestart.OverridePlebStop));

				// Remove from scheduled restarts
				updatedScheduledRestarts = updatedScheduledRestarts.Remove(walletId);
			}
		}

		if (updatedScheduledRestarts != state.ScheduledRestarts)
		{
			return state with { ScheduledRestarts = updatedScheduledRestarts };
		}

		return state;
	}


	private ManagerState HandleWalletEnteredSendWorkflow(WalletEnteredSendWorkflowMsg msg, ManagerState state)
	{
		if (state.CoinJoinClientStates.TryGetValue(msg.WalletId, out var stateHolder))
		{
			var blockedState = new UiBlockedStateHolder(
				NeedRestart: false,
				stateHolder.StopWhenAllMixed,
				stateHolder.OverridePlebStop,
				stateHolder.OutputWallet);

			return state with
			{
				WalletsBlockedByUi = state.WalletsBlockedByUi.SetItem(msg.WalletId, blockedState)
			};
		}
		return state;
	}

	private ManagerState HandleWalletLeftSendWorkflow(WalletLeftSendWorkflowMsg msg, ManagerState state)
	{
		var wallet = msg.Wallet;

		if (!state.WalletsBlockedByUi.TryGetValue(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug(FormatLog("Wallet was not in send workflow but left it.", wallet));
			return state;
		}

		if (stateHolder.NeedRestart)
		{
			// Post start message
			_mailboxProcessor?.Post(new StartCoinJoin(
				wallet,
				stateHolder.OutputWallet,
				stateHolder.StopWhenAllMixed,
				stateHolder.OverridePlebStop));
		}

		return state with
		{
			WalletsBlockedByUi = state.WalletsBlockedByUi.Remove(wallet.WalletId)
		};
	}

	private Task<ManagerState> HandleWalletEnteredSendingAsync(WalletEnteredSendingMsg msg, ManagerState state)
	{
		var wallet = msg.Wallet;

		if (!state.WalletsBlockedByUi.ContainsKey(wallet.WalletId))
		{
			Logger.LogDebug(FormatLog("Wallet tried to enter sending but it was not in the send workflow.", wallet));
			return Task.FromResult(state);
		}

		if (!state.CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug(FormatLog("Wallet tried to enter sending but state was missing.", wallet));
			return Task.FromResult(state);
		}

		// Evaluate and set if we should restart after the send workflow
		if (stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
		{
			var updatedBlockedState = new UiBlockedStateHolder(
				NeedRestart: true,
				stateHolder.StopWhenAllMixed,
				stateHolder.OverridePlebStop,
				stateHolder.OutputWallet);

			// Post stop message
			_mailboxProcessor?.Post(new StopCoinJoin(wallet));

			return Task.FromResult(state with
			{
				WalletsBlockedByUi = state.WalletsBlockedByUi.SetItem(wallet.WalletId, updatedBlockedState)
			});
		}

		return Task.FromResult(state);
	}

	private async Task<ManagerState> HandleSignalStopAllCoinjoinsAsync(ManagerState state, CancellationToken ct)
	{
		var wallets = await _walletProvider.GetWalletsAsync().ConfigureAwait(false);
		var updatedBlockedByUi = state.WalletsBlockedByUi;

		foreach (var wallet in wallets)
		{
			if (state.CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder) &&
				stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
			{
				var blockedState = new UiBlockedStateHolder(
					true,
					stateHolder.StopWhenAllMixed,
					stateHolder.OverridePlebStop,
					stateHolder.OutputWallet);

				updatedBlockedByUi = updatedBlockedByUi.SetItem(wallet.WalletId, blockedState);

				// Post stop message
				_mailboxProcessor?.Post(new StopCoinJoin(wallet));
			}
		}

		return state with { WalletsBlockedByUi = updatedBlockedByUi };
	}

	private ManagerState HandleRestartAbortedCoinjoins(ManagerState state)
	{
		var updatedBlockedByUi = state.WalletsBlockedByUi;

		foreach (var (walletId, blockedState) in state.WalletsBlockedByUi)
		{
			if (blockedState.NeedRestart)
			{
				// Find the wallet
				if (state.TrackedWallets.TryGetValue(walletId, out var wallet))
				{
					// Post start message
					_mailboxProcessor?.Post(new StartCoinJoin(
						wallet,
						blockedState.OutputWallet,
						blockedState.StopWhenAllMixed,
						blockedState.OverridePlebStop));

					updatedBlockedByUi = updatedBlockedByUi.Remove(walletId);
				}
			}
		}

		return state with { WalletsBlockedByUi = updatedBlockedByUi };
	}

	private ManagerState HandleGetCoinJoinStateQuery(GetCoinJoinState query, ManagerState state)
	{
		if (state.CoinJoinClientStates.TryGetValue(query.WalletId, out var stateHolder))
		{
			query.Reply.Reply(stateHolder.CoinJoinClientState);
		}
		else
		{
			query.Reply.Reply(CoinJoinClientState.Idle);
		}
		return state;
	}

	#endregion New MailboxProcessor Architecture

private record CoinSelectionResult(SmartCoin[] CandidateCoins, SmartCoin[] BannedCoins, SmartCoin[] ImmatureCoins, SmartCoin[] UnconfirmedCoins, SmartCoin[] ExcludedCoins)
{
    public CoinSelectionResult() : this([], [], [], [], []) { }
}

private async Task<CoinSelectionResult> GetCoinSelectionAsync(IWallet wallet)
{
    var coinCandidates = new CoinsView(await wallet.GetCoinjoinCoinCandidatesAsync().ConfigureAwait(false))
        .Available()
        .Where(x => !_coinRefrigerator.IsFrozen(x))
        .ToArray();

    if (coinCandidates.Length == 0)
    {
        return new CoinSelectionResult();
    }

    var bannedCoins = coinCandidates.Where(x => _coinPrison.IsBanned(x.Outpoint)).ToArray();
    var immatureCoins = _serverTipHeight > 0
	    ? coinCandidates.Where(x => x.Transaction.IsImmature(_serverTipHeight)).ToArray()
	    : [];
    var unconfirmedCoins = coinCandidates.Where(x => !x.Confirmed).ToArray();
    var excludedCoins = coinCandidates.Where(x => x.IsExcludedFromCoinJoin).ToArray();

    var availableCoins = coinCandidates
        .Except(bannedCoins)
        .Except(immatureCoins)
        .Except(unconfirmedCoins)
        .Except(excludedCoins)
        .ToArray();

    return new CoinSelectionResult(
        availableCoins,
        bannedCoins,
        immatureCoins,
        unconfirmedCoins,
        excludedCoins);
}

private async Task<CoinSelectionResult> SelectCandidateCoinsAsync(IWallet wallet)
{
    var result = await GetCoinSelectionAsync(wallet).ConfigureAwait(false);

    if (result.CandidateCoins.Length > 0)
    {
	    return result;
    }

    var anyNonPrivateUnconfirmed = result.UnconfirmedCoins.Any(x => !x.IsPrivate(wallet.AnonScoreTarget));
    var anyNonPrivateImmature = result.ImmatureCoins.Any(x => !x.IsPrivate(wallet.AnonScoreTarget));
    var anyNonPrivateBanned = result.BannedCoins.Any(x => !x.IsPrivate(wallet.AnonScoreTarget));
    var anyNonPrivateExcluded = result.ExcludedCoins.Any(x => !x.IsPrivate(wallet.AnonScoreTarget));

    var errorMessage = $"Coin candidates are empty! {nameof(anyNonPrivateUnconfirmed)}:{anyNonPrivateUnconfirmed} " +
                       $"{nameof(anyNonPrivateImmature)}:{anyNonPrivateImmature} " +
                       $"{nameof(anyNonPrivateBanned)}:{anyNonPrivateBanned} " +
                       $"{nameof(anyNonPrivateExcluded)}:{anyNonPrivateExcluded}";

    if (anyNonPrivateUnconfirmed)
    {
	    throw new CoinJoinClientException(CoinjoinError.NoConfirmedCoinsEligibleToMix, errorMessage);
    }

    if (anyNonPrivateImmature)
    {
	    throw new CoinJoinClientException(CoinjoinError.OnlyImmatureCoinsAvailable, errorMessage);
    }

    if (anyNonPrivateBanned)
    {
	    throw new CoinJoinClientException(CoinjoinError.CoinsRejected, errorMessage);
    }

    if (anyNonPrivateExcluded)
    {
	    throw new CoinJoinClientException(CoinjoinError.OnlyExcludedCoinsAvailable, errorMessage);
    }

    throw new CoinJoinClientException(CoinjoinError.NoCoinsEligibleToMix, "No candidate coins available to mix.");

}

	/// <summary>
	/// Mark all the outputs we had in any of our wallets used.
	/// </summary>
	private void MarkDestinationsUsed(IDestinationProvider destinationProvider, ImmutableList<Script> outputs)
	{
		destinationProvider.TrySetScriptStates(KeyState.Used, outputs.ToHashSet());
	}

	private void NotifyWalletStartedCoinJoin(IWallet openedWallet) =>
		StatusChanged.SafeInvoke(this, new WalletStartedCoinJoinEventArgs(openedWallet));

	private void NotifyWalletStoppedCoinJoin(IWallet openedWallet) =>
	StatusChanged.SafeInvoke(this, new WalletStoppedCoinJoinEventArgs(openedWallet));

	private void NotifyCoinJoinStarted(IWallet openedWallet, TimeSpan registrationTimeout) =>
		StatusChanged.SafeInvoke(this, new StartedEventArgs(openedWallet, registrationTimeout));

	private void NotifyCoinJoinStartError(IWallet openedWallet, CoinjoinError error) =>
		StatusChanged.SafeInvoke(this, new StartErrorEventArgs(openedWallet, error));

	private void NotifyMixableWalletUnloaded(IWallet closedWallet) =>
		StatusChanged.SafeInvoke(this, new StoppedEventArgs(closedWallet, StopReason.WalletUnloaded));

	private void NotifyMixableWalletLoaded(IWallet openedWallet) =>
		StatusChanged.SafeInvoke(this, new LoadedEventArgs(openedWallet));

	private void NotifyCoinJoinCompletion(CoinJoinTracker finishedCoinJoin)
	{
		CompletionStatus status = finishedCoinJoin.CoinJoinTask.Status switch
		{
			TaskStatus.RanToCompletion when finishedCoinJoin.CoinJoinTask.Result is SuccessfulCoinJoinResult => CompletionStatus.Success,
			TaskStatus.Canceled => CompletionStatus.Canceled,
			TaskStatus.Faulted => CompletionStatus.Failed,
			_ => CompletionStatus.Unknown
		};

		CompletedEventArgs e = new(finishedCoinJoin.Wallet, status);
		StatusChanged.SafeInvoke(this, e);
	}

	private void NotifyCoinJoinStatusChanged(IWallet wallet, CoinJoinProgressEventArgs coinJoinProgressEventArgs) =>
		StatusChanged.SafeInvoke(
			this,
			new CoinJoinStatusEventArgs(wallet, coinJoinProgressEventArgs));

	private async Task<ImmutableDictionary<WalletId, IWallet>> GetMixableWalletsAsync() =>
		(await _walletProvider.GetWalletsAsync().ConfigureAwait(false))
			.Where(x => x.IsMixable)
			.ToImmutableDictionary(x => x.WalletId, x => x);


	public void WalletEnteredSendWorkflow(WalletId walletId)
	{
		_mailboxProcessor?.Post(new WalletEnteredSendWorkflowMsg(walletId));
	}

	public void WalletLeftSendWorkflow(Wallet wallet)
	{
		_mailboxProcessor?.Post(new WalletLeftSendWorkflowMsg(wallet));
	}

	public async Task WalletEnteredSendingAsync(Wallet wallet)
	{
		_mailboxProcessor?.Post(new WalletEnteredSendingMsg(wallet));
	}

	public async Task SignalToStopCoinjoinsAsync()
	{
		_mailboxProcessor?.Post(new SignalStopAllCoinjoins());
	}

	public async Task RestartAbortedCoinjoinsAsync()
	{
		_mailboxProcessor?.Post(new RestartAbortedCoinjoins());
	}

	private void CoinJoinTracker_WalletCoinJoinProgressChanged(object? sender, CoinJoinProgressEventArgs e)
	{
		if (sender is not IWallet wallet)
		{
			throw new InvalidOperationException("Sender must be a wallet.");
		}

		NotifyCoinJoinStatusChanged(wallet, e);
	}

	public override void Dispose()
	{
		_mailboxProcessor?.Dispose();
		_serverTipHeightChangeSubscription.Dispose();
		base.Dispose();
	}

	// MailboxProcessor state - consolidates all manager state into immutable structure
	private record ManagerState(
		ImmutableDictionary<WalletId, IWallet> TrackedWallets,
		ImmutableDictionary<WalletId, CoinJoinTracker> TrackedCoinJoins,
		ImmutableDictionary<WalletId, ScheduledRestart> ScheduledRestarts,
		ImmutableDictionary<WalletId, UiBlockedStateHolder> WalletsBlockedByUi,
		ImmutableDictionary<WalletId, CoinJoinClientStateHolder> CoinJoinClientStates,
		ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> CoinsInCriticalPhase)
	{
		public static ManagerState Empty => new(
			ImmutableDictionary<WalletId, IWallet>.Empty,
			ImmutableDictionary<WalletId, CoinJoinTracker>.Empty,
			ImmutableDictionary<WalletId, ScheduledRestart>.Empty,
			ImmutableDictionary<WalletId, UiBlockedStateHolder>.Empty,
			ImmutableDictionary<WalletId, CoinJoinClientStateHolder>.Empty,
			ImmutableDictionary<WalletId, ImmutableList<SmartCoin>>.Empty);
	}

	// Represents a scheduled restart in state (replaces TrackedAutoStart)
	private record ScheduledRestart(
		IWallet Wallet,
		IWallet OutputWallet,
		bool StopWhenAllMixed,
		bool OverridePlebStop,
		DateTimeOffset ScheduledFor,
		Guid ScheduleId);

	private record CoinJoinClientStateHolder(CoinJoinClientState CoinJoinClientState, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet);
	private record UiBlockedStateHolder(bool NeedRestart, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet);
}

public record CoinJoinConfiguration(string CoordinatorIdentifier,  decimal MaxCoinJoinMiningFeeRate, int AbsoluteMinInputCount, bool AllowSoloCoinjoining);
