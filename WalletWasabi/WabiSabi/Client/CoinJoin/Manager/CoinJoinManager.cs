using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using WalletWasabi.Exceptions;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using static WalletWasabi.Logging.LoggerTools;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager;

public delegate Task<IEnumerable<Wallet>> WalletProvider();

public class CoinJoinManager : BackgroundService
{
	public CoinJoinManager(
		WalletProvider getWallets,
		RoundStateProvider roundStatusProvider,
		Func<string, IWabiSabiApiRequestHandler> arenaRequestHandlerFactory,
		CoinJoinConfiguration coinJoinConfiguration,
		CoinPrison coinPrison,
		EventBus eventBus)
	{
		_state = new();
		_getWalletsAsync = getWallets;
		ArenaRequestHandlerFactory = arenaRequestHandlerFactory;
		_roundStatusProvider = roundStatusProvider;
		_coinJoinConfiguration = coinJoinConfiguration;
		_coinPrison = coinPrison;
		_serverTipHeightChangeSubscription = eventBus.Subscribe<NetworkTipHeightChanged>(h => _serverTipHeight = h.Height);
		_mailboxProcessor = new MailboxProcessor<CoinJoinCommand>(nameof(CoinJoinManager), HandleCoinJoinCommandsAsync, cancellationToken: _stopCts.Token);
	}

	public event EventHandler<StatusChangedEventArgs>? StatusChanged;

	private readonly ManagerState _state;
	public ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> CoinsInCriticalPhase => _state.CoinsInCriticalPhase;
	private readonly WalletProvider _getWalletsAsync;
	private Func<string, IWabiSabiApiRequestHandler> ArenaRequestHandlerFactory { get; }
	private readonly RoundStateProvider _roundStatusProvider;
	private readonly CoinPrison _coinPrison;
	private readonly CoinRefrigerator _coinRefrigerator = new();
	private readonly CoinJoinConfiguration _coinJoinConfiguration;
	private uint _serverTipHeight;
	private readonly MailboxProcessor<CoinJoinCommand> _mailboxProcessor;
	private readonly CancellationTokenSource _stopCts = new();

	public CoinJoinClientState HighestCoinJoinClientState => _state.CoinJoinClientStates.Values.Any()
		? _state.CoinJoinClientStates.Values.Select(x => x.CoinJoinClientState).MaxBy(s => (int)s)
		: CoinJoinClientState.Idle;

	private readonly IDisposable _serverTipHeightChangeSubscription;

	private static bool IsUnderPlebStop(SmartCoin[] coinCandidates, Money plebStopThreshold) => coinCandidates.Sum(x => x.Amount) < plebStopThreshold;

	#region Public API (Start | Stop | TryGetWalletStatus)

	public void RequestCoinJoinStart(Wallet wallet, Wallet outputWallet, bool stopWhenAllMixed, bool overridePlebStop)
	{
		var coinSelectionResult = GetCoinSelection(wallet);
		var coinCandidates = coinSelectionResult.CandidateCoins;

		if (overridePlebStop && !IsUnderPlebStop(coinCandidates, wallet.PlebStopThreshold))
		{
			// Turn off overriding if we reached or exceeded the threshold meanwhile.
			overridePlebStop = false;
			Logger.LogDebug("Do not override PlebStop anymore, confirmed balance no longer below the threshold.", wallet);
		}

		var command = new StartCoinJoinCommand(wallet, outputWallet, stopWhenAllMixed, overridePlebStop);
		_mailboxProcessor.Post(command);
	}

	public void RequestCoinJoinStop(Wallet wallet)
	{
		_mailboxProcessor.Post(new StopCoinJoinCommand(wallet));
	}

	public CoinJoinClientState GetCoinjoinClientState(WalletId walletId)
	{
		if (_state.CoinJoinClientStates.TryGetValue(walletId, out var coinJoinClientStateHolder))
		{
			return coinJoinClientStateHolder.CoinJoinClientState;
		}
		throw new ArgumentException($"Wallet {walletId} is not tracked.");
	}

	#endregion Public API (Start | Stop | TryGetWalletStatus)

	public override Task StartAsync(CancellationToken cancellationToken)
	{
		_mailboxProcessor.Start();
		return base.StartAsync(cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var monitorCoinJoinTask = Task.Run(() => MonitorAndHandlingCoinJoinFinalizationAsync(stoppingToken), stoppingToken);

		await WaitAndHandleResultOfTasksAsync(nameof(monitorCoinJoinTask), monitorCoinJoinTask).ConfigureAwait(false);
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		_stopCts.Cancel();
		return base.StopAsync(cancellationToken);
	}

	private async Task HandleCoinJoinCommandsAsync(Mailbox<CoinJoinCommand> mailbox, CancellationToken cancellationToken)
	{
		var coinJoinTrackerFactory = new CoinJoinTrackerFactory(ArenaRequestHandlerFactory, _roundStatusProvider, _coinJoinConfiguration, cancellationToken);

		// TODO: Use Workers.EventDriven once we get state ready for it.
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var command = await mailbox.ReceiveAsync(cancellationToken).ConfigureAwait(false);

				switch (command)
				{
					case StartCoinJoinCommand startCommand:
						HandleStartCoinJoinCommand(startCommand, coinJoinTrackerFactory);
						break;

					case StopCoinJoinCommand stopCommand:
						HandleStopCoinJoinCommand(stopCommand);
						break;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				Logger.LogDebug("Handling of commands in CoinJoinManager was stopped.");
			}
			catch (Exception e)
			{
				Logger.LogError($"Error while handling CoinJoin command: {e}");
			}
		}

		foreach (var trackedAutoStart in _state.TrackedAutoStarts.Values)
		{
			trackedAutoStart.CancellationTokenSource.Cancel();
			trackedAutoStart.CancellationTokenSource.Dispose();
		}

		await WaitAndHandleResultOfTasksAsync(nameof(_state.TrackedAutoStarts), _state.TrackedAutoStarts.Values.Select(x => x.Task).ToArray()).ConfigureAwait(false);
	}

	private void HandleStartCoinJoinCommand(StartCoinJoinCommand startCommand, CoinJoinTrackerFactory coinJoinTrackerFactory)
	{
		var walletToStart = startCommand.Wallet;

		if (_state.TrackedCoinJoins.TryGetValue(walletToStart.WalletId, out var tracker))
		{
			if (startCommand.StopWhenAllMixed != tracker.StopWhenAllMixed)
			{
				tracker.StopWhenAllMixed = startCommand.StopWhenAllMixed;
				Logger.LogDebug(FormatLog($"Cannot start coinjoin, because it is already running - but updated the value of {nameof(startCommand.StopWhenAllMixed)} to {startCommand.StopWhenAllMixed}.", walletToStart));
			}
			else
			{
				Logger.LogDebug(FormatLog("Cannot start coinjoin, because it is already running.", walletToStart));
			}

			// On cancelling the shutdown prevention, we need to set it back to false, otherwise we won't continue CJing.
			tracker.IsStopped = false;

			return;
		}

		IEnumerable<SmartCoin> SanityChecksAndGetCoinCandidatesFunc()
		{
			if (_state.WalletsBlockedByUi.ContainsKey(walletToStart.WalletId))
			{
				throw new CoinJoinClientException(CoinjoinError.UserInSendWorkflow);
			}

			var coinSelectionResult = SelectCandidateCoins(walletToStart);
			var coinCandidates = coinSelectionResult.CandidateCoins;

			if (IsUnderPlebStop(coinCandidates, walletToStart.PlebStopThreshold) && !startCommand.OverridePlebStop)
			{
				Logger.LogTrace(FormatLog("PlebStop preventing coinjoin.", walletToStart));

				if (!IsUnderPlebStop(coinCandidates.Union(coinSelectionResult.UnconfirmedCoins).ToArray(), walletToStart.PlebStopThreshold))
				{
					throw new CoinJoinClientException(CoinjoinError.NotEnoughConfirmedUnprivateBalance);
				}

				throw new CoinJoinClientException(CoinjoinError.NotEnoughUnprivateBalance);
			}

			// If there are pending payments, ignore already achieved privacy.
			if (!walletToStart.BatchedPayments.AreTherePendingPayments)
			{
				// If all coins are already private, then don't mix.
				if (walletToStart.IsWalletPrivate())
				{
					Logger.LogTrace(FormatLog("All mixed!", walletToStart));
					throw new CoinJoinClientException(CoinjoinError.AllCoinsPrivate);
				}

				// If all coin candidates are private it makes no sense to mix.
				if (coinCandidates.All(x => x.IsPrivate(walletToStart.AnonScoreTarget)))
				{
					throw new CoinJoinClientException(
						CoinjoinError.NoCoinsEligibleToMix,
						$"All coin candidates are already private and {nameof(startCommand.StopWhenAllMixed)} was {startCommand.StopWhenAllMixed}");
				}
			}

			NotifyWalletStartedCoinJoin(walletToStart);

			return coinCandidates;
		}

		var coinJoinTracker = coinJoinTrackerFactory.CreateAndStart(walletToStart, startCommand.OutputWallet, SanityChecksAndGetCoinCandidatesFunc, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop);

		if (!_state.TrackedCoinJoins.TryAdd(walletToStart.WalletId, coinJoinTracker))
		{
			// This should never happen.
			Logger.LogError(FormatLog($"{nameof(CoinJoinTracker)} was already added.", walletToStart));
			coinJoinTracker.Stop();
			coinJoinTracker.Dispose();
			return;
		}

		coinJoinTracker.WalletCoinJoinProgressChanged += CoinJoinTracker_WalletCoinJoinProgressChanged;

		var registrationTimeout = TimeSpan.MaxValue;
		NotifyCoinJoinStarted(walletToStart, registrationTimeout);

		Logger.LogDebug(FormatLog($"{nameof(CoinJoinClient)} started.", walletToStart));
		Logger.LogDebug(FormatLog($"{nameof(startCommand.StopWhenAllMixed)}:'{startCommand.StopWhenAllMixed}' {nameof(startCommand.OverridePlebStop)}:'{startCommand.OverridePlebStop}'.", walletToStart));

		// In case there was another start scheduled just remove it.
		TryRemoveTrackedAutoStart(_state.TrackedAutoStarts, walletToStart);
	}

	private void HandleStopCoinJoinCommand(StopCoinJoinCommand stopCommand)
	{
		var walletToStop = stopCommand.Wallet;

		var autoStartRemoved = TryRemoveTrackedAutoStart(_state.TrackedAutoStarts, walletToStop);

		if (_state.TrackedCoinJoins.TryGetValue(walletToStop.WalletId, out var coinJoinTrackerToStop))
		{
			coinJoinTrackerToStop.Stop();
			if (coinJoinTrackerToStop.InCriticalCoinJoinState)
			{
				Logger.LogWarning(FormatLog("Coinjoin is in critical phase, it cannot be stopped - it won't restart later.", walletToStop));
			}
		}
		else if (autoStartRemoved)
		{
			NotifyWalletStoppedCoinJoin(walletToStop);
		}
	}

	private record CoinSelectionResult(SmartCoin[] CandidateCoins, SmartCoin[] BannedCoins, SmartCoin[] ImmatureCoins, SmartCoin[] UnconfirmedCoins, SmartCoin[] ExcludedCoins)
	{
		public CoinSelectionResult() : this([], [], [], [], []) { }
	}

	private CoinSelectionResult GetCoinSelection(Wallet wallet)
	{
		var coinCandidates = new CoinsView(wallet.GetCoinjoinCoinCandidates())
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

	private CoinSelectionResult SelectCandidateCoins(Wallet wallet)
	{
		var result = GetCoinSelection(wallet);

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

	private bool TryRemoveTrackedAutoStart(ConcurrentDictionary<WalletId, TrackedAutoStart> trackedAutoStarts, Wallet wallet)
	{
		if (trackedAutoStarts.TryRemove(wallet.WalletId, out var trackedAutoStart))
		{
			trackedAutoStart.CancellationTokenSource.Cancel();
			trackedAutoStart.CancellationTokenSource.Dispose();
			return true;
		}
		return false;
	}

	private void ScheduleRestartAutomatically(Wallet walletToStart, ConcurrentDictionary<WalletId, TrackedAutoStart> trackedAutoStarts, bool stopWhenAllMixed, bool overridePlebStop, Wallet outputWallet, CancellationToken stoppingToken)
	{
		var skipDelay = false;
		if (trackedAutoStarts.TryGetValue(walletToStart.WalletId, out var trackedAutoStart))
		{
			if (stopWhenAllMixed == trackedAutoStart.StopWhenAllMixed && overridePlebStop == trackedAutoStart.OverridePlebStop && outputWallet.WalletId == trackedAutoStart.OutputWallet.WalletId)
			{
				Logger.LogDebug(FormatLog("AutoStart was already scheduled", walletToStart));
				return;
			}

			Logger.LogDebug(FormatLog("AutoStart was already scheduled with different parameters, cancel the last task and do not wait.", walletToStart));
			TryRemoveTrackedAutoStart(trackedAutoStarts, walletToStart);
			skipDelay = true;
		}

		NotifyWalletStartedCoinJoin(walletToStart);

#pragma warning disable CA2000 // Dispose objects before losing scope
		var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
#pragma warning restore CA2000 // Dispose objects before losing scope

		var restartTask = new Task(
			async () =>
			{
				try
				{
					if (!skipDelay)
					{
						await Task.Delay(TimeSpan.FromSeconds(30), linkedCts.Token).ConfigureAwait(false);
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
				finally
				{
					linkedCts.Dispose();
				}

				if (trackedAutoStarts.TryRemove(walletToStart.WalletId, out _))
				{
					RequestCoinJoinStart(walletToStart, outputWallet, stopWhenAllMixed, overridePlebStop);
				}
				else
				{
					Logger.LogInfo(FormatLog("AutoStart was already handled.", walletToStart));
				}
			},
			linkedCts.Token);

		if (trackedAutoStarts.TryAdd(walletToStart.WalletId, new TrackedAutoStart(restartTask, stopWhenAllMixed, overridePlebStop, outputWallet, linkedCts)))
		{
			restartTask.Start();
		}
		else
		{
			Logger.LogInfo(FormatLog("AutoCoinJoin task was already added.", walletToStart));
		}
	}

	private async Task MonitorAndHandlingCoinJoinFinalizationAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			// Handles coinjoin finalization and notification.
			var finishedCoinJoins = _state.TrackedCoinJoins
				.Where(x => x.Value.IsCompleted)
				.Select(x => x.Value)
				.ToImmutableArray();

			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				await HandleCoinJoinFinalizationAsync(finishedCoinJoin, stoppingToken).ConfigureAwait(false);
			}

			// Updates coinjoin client states.
			var wallets = await _getWalletsAsync().ConfigureAwait(false);

			_state.CoinJoinClientStates = GetCoinJoinClientStates(wallets);
			_state.CoinsInCriticalPhase = GetCoinsInCriticalPhase(wallets);

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
		}
	}

	private ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> GetCoinsInCriticalPhase(IEnumerable<Wallet> wallets)
	{
		var coinsUsedInCoinjoins = ImmutableDictionary.CreateBuilder<WalletId, ImmutableList<SmartCoin>>();

		foreach (var wallet in wallets)
		{
			ImmutableList<SmartCoin> coinsInCoinjoin = [];

			if (_state.TrackedCoinJoins.TryGetValue(wallet.WalletId, out var coinJoinTracker) && !coinJoinTracker.IsCompleted)
			{
				coinsInCoinjoin = coinJoinTracker.CoinsInCriticalPhase;
			}

			coinsUsedInCoinjoins.Add(wallet.WalletId, coinsInCoinjoin);
		}

		return coinsUsedInCoinjoins.ToImmutable();
	}

	private ImmutableDictionary<WalletId, CoinJoinClientStateHolder> GetCoinJoinClientStates(IEnumerable<Wallet> wallets)
	{
		var coinJoinClientStates = ImmutableDictionary.CreateBuilder<WalletId, CoinJoinClientStateHolder>();
		foreach (var wallet in wallets)
		{
			CoinJoinClientStateHolder clientState = new(CoinJoinClientState.Idle, StopWhenAllMixed: true, OverridePlebStop: false, OutputWallet: wallet);

			if (_state.TrackedCoinJoins.TryGetValue(wallet.WalletId, out var coinJoinTracker) && !coinJoinTracker.IsCompleted)
			{
				var trackerState = coinJoinTracker.InCriticalCoinJoinState
					? CoinJoinClientState.InCriticalPhase
					: CoinJoinClientState.InProgress;

				clientState = new(trackerState, coinJoinTracker.StopWhenAllMixed, coinJoinTracker.OverridePlebStop, coinJoinTracker.OutputWallet);
			}
			else if (_state.TrackedAutoStarts.TryGetValue(wallet.WalletId, out var autoStartTracker))
			{
				clientState = new(CoinJoinClientState.InSchedule, autoStartTracker.StopWhenAllMixed, autoStartTracker.OverridePlebStop, autoStartTracker.OutputWallet);
			}

			coinJoinClientStates.Add(wallet.WalletId, clientState);
		}

		return coinJoinClientStates.ToImmutable();
	}

	private async Task HandleCoinJoinFinalizationAsync(CoinJoinTracker finishedCoinJoin, CancellationToken cancellationToken)
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
				var coinjoinTxId = successfulCoinjoin.UnsignedCoinJoin.GetHash();
				var paymentsTotal = Money.Satoshis(batchedPayments.GetPayments().Where(p => p.State is InProgressPayment).Sum(p => p.Amount));
				_coinRefrigerator.Freeze(successfulCoinjoin.Coins);
				batchedPayments.MovePaymentsToFinished(coinjoinTxId);
				MarkDestinationsUsed(destinationProvider, successfulCoinjoin.OutputScripts);
				wallet.KeyManager.AddCoinjoinCosts(new CoinjoinCosts(coinjoinTxId, successfulCoinjoin.MiningFee, successfulCoinjoin.WastedDust, paymentsTotal));
				Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was broadcast.", wallet));
			}
			else
			{
				Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was not broadcast.", wallet));
			}
		}
		catch (UnknownRoundEndingException ex)
		{
			// Assuming that the round might be broadcast but our client was not able to get the ending status.
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
			// `UnexpectedRoundPhaseException` indicates an error in the protocol however,
			// temporarily we are shortening the circuit by aborting the rounds if
			// there are Alices that didn't confirm.
			// The fix is already done but the clients have to upgrade.
			Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} failed with exception: '{e}'", wallet));
		}
		catch (WabiSabiProtocolException wpe) when (wpe.ErrorCode == WabiSabiProtocolErrorCode.WrongPhase)
		{
			// This can happen when the coordinator aborts the round in Signing phase because of detected double spend.
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

		// If any coins were marked for banning, store them to file
		if (finishedCoinJoin.BannedCoins.Count != 0)
		{
			foreach (var info in finishedCoinJoin.BannedCoins)
			{
				_coinPrison.Ban(info.Coin, info.BanUntilUtc);
			}
		}

		NotifyCoinJoinCompletion(finishedCoinJoin);

		// When to stop mixing:
		// - If stop was requested by user.
		// - If cancellation was requested.
		if (forceStop
			|| finishedCoinJoin.IsStopped
			|| cancellationToken.IsCancellationRequested)
		{
			NotifyWalletStoppedCoinJoin(wallet);
		}
		else if (wallet.IsWalletPrivate())
		{
			NotifyCoinJoinStartError(wallet, CoinjoinError.AllCoinsPrivate);
			if (!finishedCoinJoin.StopWhenAllMixed)
			{
				// In auto CJ mode we never stop trying.
				ScheduleRestartAutomatically(wallet, _state.TrackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
			}
			else
			{
				// We finished with CJ permanently.
				NotifyWalletStoppedCoinJoin(wallet);
			}
		}
		else if (cjClientException is not null)
		{
			// - If there was a CjClient exception, for example PlebStop or no coins to mix,
			// Keep trying, so CJ starts automatically when the wallet becomes mixable again.
			ScheduleRestartAutomatically(wallet, _state.TrackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
			NotifyCoinJoinStartError(wallet, cjClientException.CoinjoinError);
		}
		else
		{
			Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} restart automatically.", wallet));

			ScheduleRestartAutomatically(wallet, _state.TrackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
		}

		if (!_state.TrackedCoinJoins.TryRemove(wallet.WalletId, out _))
		{
			Logger.LogWarning(FormatLog("Was not removed from tracked wallet list. Will retry in a few seconds.", wallet));
		}
		else
		{
			finishedCoinJoin.WalletCoinJoinProgressChanged -= CoinJoinTracker_WalletCoinJoinProgressChanged;
			finishedCoinJoin.Dispose();
		}
	}

	/// <summary>
	/// Mark all the outputs we had in any of our wallets used.
	/// </summary>
	private void MarkDestinationsUsed(IDestinationProvider destinationProvider, ImmutableList<Script> outputs)
	{
		destinationProvider.TrySetScriptStates(KeyState.Used, outputs);
	}

	private void NotifyWalletStartedCoinJoin(Wallet openedWallet) =>
		StatusChanged.SafeInvoke(this, new WalletStartedCoinJoinEventArgs(openedWallet));

	private void NotifyWalletStoppedCoinJoin(Wallet openedWallet) =>
	StatusChanged.SafeInvoke(this, new WalletStoppedCoinJoinEventArgs(openedWallet));

	private void NotifyCoinJoinStarted(Wallet openedWallet, TimeSpan registrationTimeout) =>
		StatusChanged.SafeInvoke(this, new StartedEventArgs(openedWallet, registrationTimeout));

	private void NotifyCoinJoinStartError(Wallet openedWallet, CoinjoinError error) =>
		StatusChanged.SafeInvoke(this, new StartErrorEventArgs(openedWallet, error));

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

	private void NotifyCoinJoinStatusChanged(Wallet wallet, CoinJoinProgressEventArgs coinJoinProgressEventArgs) =>
		StatusChanged.SafeInvoke(
			this,
			new CoinJoinStatusEventArgs(wallet, coinJoinProgressEventArgs));

	private static async Task WaitAndHandleResultOfTasksAsync(string logPrefix, params Task[] tasks)
	{
		foreach (var task in tasks)
		{
			try
			{
				await task.ConfigureAwait(false);
				Logger.LogInfo($"Task '{logPrefix}' finished successfully.");
			}
			catch (OperationCanceledException)
			{
				Logger.LogInfo($"Task '{logPrefix}' finished successfully by cancellation.");
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Task '{logPrefix}' finished but with an error: '{ex}'.");
			}
		}
	}

	public void WalletEnteredSendWorkflow(WalletId walletId)
	{
		if (_state.CoinJoinClientStates.TryGetValue(walletId, out var stateHolder))
		{
			_state.WalletsBlockedByUi.TryAdd(walletId, new UiBlockedStateHolder(NeedRestart: false, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet));
		}
	}

	public void WalletLeftSendWorkflow(Wallet wallet)
	{
		if (!_state.WalletsBlockedByUi.TryRemove(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug(FormatLog("Wallet was not in send workflow but left it.", wallet));
			return;
		}

		if (stateHolder.NeedRestart)
		{
			Task.Run(async () => RequestCoinJoinStart(wallet, stateHolder.OutputWallet, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop));
		}
	}

	public async Task WalletEnteredSendingAsync(Wallet wallet)
	{
		if (!_state.WalletsBlockedByUi.ContainsKey(wallet.WalletId))
		{
			Logger.LogDebug(FormatLog("Wallet tried to enter sending but it was not in the send workflow.", wallet));
			return;
		}

		if (!_state.CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug(FormatLog("Wallet tried to enter sending but state was missing.", wallet));
			return;
		}

		// Evaluate and set if we should restart after the send workflow.
		if (stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
		{
			_state.WalletsBlockedByUi[wallet.WalletId] = new UiBlockedStateHolder(NeedRestart: true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet);
		}

		RequestCoinJoinStop(wallet);
	}

	public async Task SignalToStopCoinjoinsAsync()
	{
		foreach (var wallet in await _getWalletsAsync().ConfigureAwait(false))
		{
			if (_state.CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder) && stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
			{
				if (!_state.WalletsBlockedByUi.TryAdd(wallet.WalletId, new UiBlockedStateHolder(true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet)))
				{
					_state.WalletsBlockedByUi[wallet.WalletId] = new UiBlockedStateHolder(true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet);
				}
				RequestCoinJoinStop(wallet);
			}
		}
	}

	public async Task RestartAbortedCoinjoinsAsync()
	{
		foreach (var wallet in await _getWalletsAsync().ConfigureAwait(false))
		{
			if (_state.WalletsBlockedByUi.TryRemove(wallet.WalletId, out var stateHolder) && stateHolder.NeedRestart)
			{
				RequestCoinJoinStart(wallet, stateHolder.OutputWallet, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop);
			}
		}
	}

	private void CoinJoinTracker_WalletCoinJoinProgressChanged(object? sender, CoinJoinProgressEventArgs e)
	{
		if (sender is not Wallet wallet)
		{
			throw new InvalidOperationException("Sender must be a wallet.");
		}

		NotifyCoinJoinStatusChanged(wallet, e);
	}

	public override void Dispose()
	{
		_mailboxProcessor.Dispose();
		_stopCts.Dispose();
		_serverTipHeightChangeSubscription.Dispose();
		base.Dispose();
	}

	private record CoinJoinCommand(Wallet Wallet);
	private record StartCoinJoinCommand(Wallet Wallet, Wallet OutputWallet, bool StopWhenAllMixed, bool OverridePlebStop) : CoinJoinCommand(Wallet);
	private record StopCoinJoinCommand(Wallet Wallet) : CoinJoinCommand(Wallet);

	private record TrackedAutoStart(Task Task, bool StopWhenAllMixed, bool OverridePlebStop, Wallet OutputWallet, CancellationTokenSource CancellationTokenSource);
	private record CoinJoinClientStateHolder(CoinJoinClientState CoinJoinClientState, bool StopWhenAllMixed, bool OverridePlebStop, Wallet OutputWallet);
	private record UiBlockedStateHolder(bool NeedRestart, bool StopWhenAllMixed, bool OverridePlebStop, Wallet OutputWallet);

	// TODO: Some properties are still missing in this record: ScheduledRestarts.
	/// <param name="WalletsBlockedByUi">
	/// The Dictionary is used for tracking the wallets that are blocked from CJs by UI.
	/// The state holder has 3 boolean value, the first one indicates if the CJ needs to be restarted or not after leaving the blocking UI dialogs.
	/// The other 2 is only needed not to loose the StopWhenAllMixed and OverridePlebStop configuration.
	/// Right now, the Shutdown prevention and the Send workflow can block the CJs.
	/// </param>
	private record ManagerState(
		ConcurrentDictionary<WalletId, CoinJoinTracker> TrackedCoinJoins,
		ConcurrentDictionary<WalletId, TrackedAutoStart> TrackedAutoStarts,
		ConcurrentDictionary<WalletId, UiBlockedStateHolder> WalletsBlockedByUi)
	{
		public ImmutableDictionary<WalletId, CoinJoinClientStateHolder> CoinJoinClientStates { get; set; } = [];
		public ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> CoinsInCriticalPhase { get; set; } = [];

		public ManagerState() : this(
			new ConcurrentDictionary<WalletId, CoinJoinTracker>(),
			new ConcurrentDictionary<WalletId, TrackedAutoStart>(),
			new ConcurrentDictionary<WalletId, UiBlockedStateHolder>())
		{
		}
	}
}

public record CoinJoinConfiguration(string CoordinatorIdentifier,  decimal MaxCoinJoinMiningFeeRate, int AbsoluteMinInputCount, bool AllowSoloCoinjoining);
