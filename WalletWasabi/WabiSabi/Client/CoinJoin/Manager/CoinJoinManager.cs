using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
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

	/// <summary>
	/// The Dictionary is used for tracking the wallets that are blocked from CJs by UI.
	/// The state holder has 3 boolean value, the first one indicates if the CJ needs to be restarted or not after leaving the blocking UI dialogs.
	/// The other 2 is only needed not to loose the StopWhenAllMixed and OverridePlebStop configuration.
	/// Right now, the Shutdown prevention and the Send workflow can block the CJs.
	/// </summary>
	private ConcurrentDictionary<WalletId, UiBlockedStateHolder> WalletsBlockedByUi { get; } = new();

	public CoinJoinClientState HighestCoinJoinClientState => CoinJoinClientStates.Values.Any()
		? CoinJoinClientStates.Values.Select(x => x.CoinJoinClientState).MaxBy(s => (int)s)
		: CoinJoinClientState.Idle;

	private ImmutableDictionary<WalletId, CoinJoinClientStateHolder> CoinJoinClientStates { get; set; } = ImmutableDictionary<WalletId, CoinJoinClientStateHolder>.Empty;

	private readonly Channel<CoinJoinCommand> _commandChannel = Channel.CreateUnbounded<CoinJoinCommand>();
	private readonly IDisposable _serverTipHeightChangeSubscription;

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

		await _commandChannel.Writer.WriteAsync(new StartCoinJoinCommand(wallet, outputWallet, stopWhenAllMixed, overridePlebStop), cancellationToken).ConfigureAwait(false);
	}

	public async Task StopAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await _commandChannel.Writer.WriteAsync(new StopCoinJoinCommand(wallet), cancellationToken).ConfigureAwait(false);
	}

	public CoinJoinClientState GetCoinjoinClientState(WalletId walletId)
	{
		if (CoinJoinClientStates.TryGetValue(walletId, out var coinJoinClientStateHolder))
		{
			return coinJoinClientStateHolder.CoinJoinClientState;
		}
		throw new ArgumentException($"Wallet {walletId} is not tracked.");
	}

	#endregion Public API (Start | Stop | TryGetWalletStatus)

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Detects and notifies about wallets that can participate in a coinjoin.
		var walletsMonitoringTask = Task.Run(() => MonitorWalletsAsync(stoppingToken), stoppingToken);

		// Coinjoin handling Start / Stop and finalization.
		var monitorAndHandleCoinjoinsTask = MonitorAndHandleCoinJoinsAsync(stoppingToken);

		await Task.WhenAny(walletsMonitoringTask, monitorAndHandleCoinjoinsTask).ConfigureAwait(false);

		await WaitAndHandleResultOfTasksAsync(nameof(walletsMonitoringTask), walletsMonitoringTask).ConfigureAwait(false);
		await WaitAndHandleResultOfTasksAsync(nameof(monitorAndHandleCoinjoinsTask), monitorAndHandleCoinjoinsTask).ConfigureAwait(false);
	}

	private async Task MonitorWalletsAsync(CancellationToken stoppingToken)
	{
		var trackedWallets = new Dictionary<WalletId, IWallet>();
		while (!stoppingToken.IsCancellationRequested)
		{
			var mixableWallets = await GetMixableWalletsAsync().ConfigureAwait(false);

			// Notifies when a wallet meets the criteria for participating in a coinjoin.
			var openedWallets = mixableWallets.Where(x => !trackedWallets.ContainsKey(x.Key)).ToImmutableList();
			foreach (var openedWallet in openedWallets.Select(x => x.Value))
			{
				trackedWallets.Add(openedWallet.WalletId, openedWallet);
				NotifyMixableWalletLoaded(openedWallet);
			}

			// Notifies when a wallet no longer meets the criteria for participating in a coinjoin.
			var closedWallets = trackedWallets.Where(x => !mixableWallets.ContainsKey(x.Key)).ToImmutableList();
			foreach (var closedWallet in closedWallets.Select(x => x.Value))
			{
				NotifyMixableWalletUnloaded(closedWallet);
				trackedWallets.Remove(closedWallet.WalletId);
			}
			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
		}
	}

	private async Task MonitorAndHandleCoinJoinsAsync(CancellationToken stoppingToken)
	{
		// This is a shared resource and that's why it is concurrent. Alternatives are locking structures,
		// using a single lock around its access or use a channel.
		var trackedCoinJoins = new ConcurrentDictionary<WalletId, CoinJoinTracker>();
		var trackedAutoStarts = new ConcurrentDictionary<IWallet, TrackedAutoStart>();

		var commandsHandlingTask = Task.Run(() => HandleCoinJoinCommandsAsync(trackedCoinJoins, trackedAutoStarts, stoppingToken), stoppingToken);
		var monitorCoinJoinTask = Task.Run(() => MonitorAndHandlingCoinJoinFinalizationAsync(trackedCoinJoins, trackedAutoStarts, stoppingToken), stoppingToken);

		await Task.WhenAny(commandsHandlingTask, monitorCoinJoinTask).ConfigureAwait(false);

		await WaitAndHandleResultOfTasksAsync(nameof(commandsHandlingTask), commandsHandlingTask).ConfigureAwait(false);
		await WaitAndHandleResultOfTasksAsync(nameof(monitorCoinJoinTask), monitorCoinJoinTask).ConfigureAwait(false);
	}

	private async Task HandleCoinJoinCommandsAsync(ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts, CancellationToken stoppingToken)
	{
		var coinJoinTrackerFactory = new CoinJoinTrackerFactory(ArenaRequestHandlerFactory, _roundStatusProvider, _coinJoinConfiguration, stoppingToken);

		async void StartCoinJoinCommand(StartCoinJoinCommand startCommand)
		{
			var walletToStart = startCommand.Wallet;

			if (trackedCoinJoins.TryGetValue(walletToStart.WalletId, out var tracker))
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

			async Task<IEnumerable<SmartCoin>> SanityChecksAndGetCoinCandidatesFunc()
			{
				if (WalletsBlockedByUi.ContainsKey(walletToStart.WalletId))
				{
					throw new CoinJoinClientException(CoinjoinError.UserInSendWorkflow);
				}

				var coinSelectionResult = await SelectCandidateCoinsAsync(walletToStart).ConfigureAwait(false);
				var coinCandidates = coinSelectionResult.CandidateCoins;

				if (IsUnderPlebStop(coinCandidates, walletToStart.PlebStopThreshold) && !startCommand.OverridePlebStop)
				{
					Logger.LogTrace(FormatLog("PlebStop preventing coinjoin.", walletToStart));

					if(!IsUnderPlebStop(coinCandidates.Union(coinSelectionResult.UnconfirmedCoins).ToArray(), walletToStart.PlebStopThreshold))
					{
						throw new CoinJoinClientException(CoinjoinError.NotEnoughConfirmedUnprivateBalance);
					}

					throw new CoinJoinClientException(CoinjoinError.NotEnoughUnprivateBalance);
				}

				// If there are pending payments, ignore already achieved privacy.
				if (!walletToStart.BatchedPayments.AreTherePendingPayments)
				{
					// If all coins are already private, then don't mix.
					if (await walletToStart.IsWalletPrivateAsync().ConfigureAwait(false))
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

			var coinJoinTracker = await coinJoinTrackerFactory.CreateAndStartAsync(walletToStart, startCommand.OutputWallet, SanityChecksAndGetCoinCandidatesFunc, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop).ConfigureAwait(false);

			if (!trackedCoinJoins.TryAdd(walletToStart.WalletId, coinJoinTracker))
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
			TryRemoveTrackedAutoStart(trackedAutoStarts, walletToStart);
		}

		void StopCoinJoinCommand(StopCoinJoinCommand stopCommand)
		{
			var walletToStop = stopCommand.Wallet;

			var autoStartRemoved = TryRemoveTrackedAutoStart(trackedAutoStarts, walletToStop);

			if (trackedCoinJoins.TryGetValue(walletToStop.WalletId, out var coinJoinTrackerToStop))
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

		while (!stoppingToken.IsCancellationRequested)
		{
			var command = await _commandChannel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);

			switch (command)
			{
				case StartCoinJoinCommand startCommand:
					StartCoinJoinCommand(startCommand);
					break;

				case StopCoinJoinCommand stopCommand:
					StopCoinJoinCommand(stopCommand);
					break;
			}
		}

		foreach (var trackedAutoStart in trackedAutoStarts.Values)
		{
			trackedAutoStart.CancellationTokenSource.Cancel();
			trackedAutoStart.CancellationTokenSource.Dispose();
		}

		await WaitAndHandleResultOfTasksAsync(nameof(trackedAutoStarts), trackedAutoStarts.Values.Select(x => x.Task).ToArray()).ConfigureAwait(false);
	}

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

	private bool TryRemoveTrackedAutoStart(ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts, IWallet wallet)
	{
		if (trackedAutoStarts.TryRemove(wallet, out var trackedAutoStart))
		{
			trackedAutoStart.CancellationTokenSource.Cancel();
			trackedAutoStart.CancellationTokenSource.Dispose();
			return true;
		}
		return false;
	}

	private void ScheduleRestartAutomatically(IWallet walletToStart, ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts, bool stopWhenAllMixed, bool overridePlebStop, IWallet outputWallet, CancellationToken stoppingToken)
	{
		var skipDelay = false;
		if (trackedAutoStarts.TryGetValue(walletToStart, out var trackedAutoStart))
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

				if (trackedAutoStarts.TryRemove(walletToStart, out _))
				{
					await StartAsync(walletToStart, outputWallet, stopWhenAllMixed, overridePlebStop, stoppingToken).ConfigureAwait(false);
				}
				else
				{
					Logger.LogInfo(FormatLog("AutoStart was already handled.", walletToStart));
				}
			},
			linkedCts.Token);

		if (trackedAutoStarts.TryAdd(walletToStart, new TrackedAutoStart(restartTask, stopWhenAllMixed, overridePlebStop, outputWallet, linkedCts)))
		{
			restartTask.Start();
		}
		else
		{
			Logger.LogInfo(FormatLog("AutoCoinJoin task was already added.", walletToStart));
		}
	}

	private async Task MonitorAndHandlingCoinJoinFinalizationAsync(ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts, CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			// Handles coinjoin finalization and notification.
			var finishedCoinJoins = trackedCoinJoins.Where(x => x.Value.IsCompleted).Select(x => x.Value).ToImmutableArray();
			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				await HandleCoinJoinFinalizationAsync(finishedCoinJoin, trackedCoinJoins, trackedAutoStarts, stoppingToken).ConfigureAwait(false);
			}

			// Updates coinjoin client states.
			var wallets = await _walletProvider.GetWalletsAsync().ConfigureAwait(false);

			CoinJoinClientStates = GetCoinJoinClientStates(wallets, trackedCoinJoins, trackedAutoStarts);
			CoinsInCriticalPhase = GetCoinsInCriticalPhase(wallets, trackedCoinJoins);

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
		}
	}

	private ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> GetCoinsInCriticalPhase(IEnumerable<IWallet> wallets, ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins)
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

	private static ImmutableDictionary<WalletId, CoinJoinClientStateHolder> GetCoinJoinClientStates(IEnumerable<IWallet> wallets, ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts)
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
			else if (trackedAutoStarts.TryGetValue(wallet, out var autoStartTracker))
			{
				state = new(CoinJoinClientState.InSchedule, autoStartTracker.StopWhenAllMixed, autoStartTracker.OverridePlebStop, autoStartTracker.OutputWallet);
			}

			coinJoinClientStates.Add(wallet.WalletId, state);
		}

		return coinJoinClientStates.ToImmutable();
	}

	private async Task HandleCoinJoinFinalizationAsync(CoinJoinTracker finishedCoinJoin, ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts, CancellationToken cancellationToken)
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
		else if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
		{
			NotifyCoinJoinStartError(wallet, CoinjoinError.AllCoinsPrivate);
			if (!finishedCoinJoin.StopWhenAllMixed)
			{
				// In auto CJ mode we never stop trying.
				ScheduleRestartAutomatically(wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
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
			ScheduleRestartAutomatically(wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
			NotifyCoinJoinStartError(wallet, cjClientException.CoinjoinError);
		}
		else
		{
			Logger.LogInfo(FormatLog($"{nameof(CoinJoinClient)} restart automatically.", wallet));

			ScheduleRestartAutomatically(wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
		}

		if (!trackedCoinJoins.TryRemove(wallet.WalletId, out _))
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
		if (CoinJoinClientStates.TryGetValue(walletId, out var stateHolder))
		{
			WalletsBlockedByUi.TryAdd(walletId, new UiBlockedStateHolder(NeedRestart: false, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet));
		}
	}

	public void WalletLeftSendWorkflow(Wallet wallet)
	{
		if (!WalletsBlockedByUi.TryRemove(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug(FormatLog("Wallet was not in send workflow but left it.", wallet));
			return;
		}

		if (stateHolder.NeedRestart)
		{
			Task.Run(async () => await StartAsync(wallet, stateHolder.OutputWallet, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, CancellationToken.None).ConfigureAwait(false));
		}
	}

	public async Task WalletEnteredSendingAsync(Wallet wallet)
	{
		if (!WalletsBlockedByUi.ContainsKey(wallet.WalletId))
		{
			Logger.LogDebug(FormatLog("Wallet tried to enter sending but it was not in the send workflow.", wallet));
			return;
		}

		if (!CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug(FormatLog("Wallet tried to enter sending but state was missing.", wallet));
			return;
		}

		// Evaluate and set if we should restart after the send workflow.
		if (stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
		{
			WalletsBlockedByUi[wallet.WalletId] = new UiBlockedStateHolder(NeedRestart: true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet);
		}

		await StopAsync(wallet, CancellationToken.None).ConfigureAwait(false);
	}

	public async Task SignalToStopCoinjoinsAsync()
	{
		foreach (var wallet in await _walletProvider.GetWalletsAsync().ConfigureAwait(false))
		{
			if (CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder) && stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
			{
				if (!WalletsBlockedByUi.TryAdd(wallet.WalletId, new UiBlockedStateHolder(true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet)))
				{
					WalletsBlockedByUi[wallet.WalletId] = new UiBlockedStateHolder(true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet);
				}
				await StopAsync((Wallet)wallet, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	public async Task RestartAbortedCoinjoinsAsync()
	{
		foreach (var wallet in await _walletProvider.GetWalletsAsync().ConfigureAwait(false))
		{
			if (WalletsBlockedByUi.TryRemove(wallet.WalletId, out var stateHolder) && stateHolder.NeedRestart)
			{
				await StartAsync(wallet, stateHolder.OutputWallet, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, CancellationToken.None).ConfigureAwait(false);
			}
		}
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
		_serverTipHeightChangeSubscription.Dispose();
		base.Dispose();
	}

	private record CoinJoinCommand(IWallet Wallet);
	private record StartCoinJoinCommand(IWallet Wallet, IWallet OutputWallet, bool StopWhenAllMixed, bool OverridePlebStop) : CoinJoinCommand(Wallet);
	private record StopCoinJoinCommand(IWallet Wallet) : CoinJoinCommand(Wallet);

	private record TrackedAutoStart(Task Task, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet, CancellationTokenSource CancellationTokenSource);
	private record CoinJoinClientStateHolder(CoinJoinClientState CoinJoinClientState, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet);
	private record UiBlockedStateHolder(bool NeedRestart, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet);
}

public record CoinJoinConfiguration(string CoordinatorIdentifier,  decimal MaxCoinJoinMiningFeeRate, int AbsoluteMinInputCount, bool AllowSoloCoinjoining);
