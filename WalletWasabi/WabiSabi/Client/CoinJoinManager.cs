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
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinManager : BackgroundService
{
	private record CoinJoinCommand(Wallet Wallet);
	private record StartCoinJoinCommand(Wallet Wallet, bool StopWhenAllMixed, bool OverridePlebStop) : CoinJoinCommand(Wallet);
	private record StopCoinJoinCommand(Wallet Wallet) : CoinJoinCommand(Wallet);

	public CoinJoinManager(WalletManager walletManager, RoundStateUpdater roundStatusUpdater, IWasabiHttpClientFactory backendHttpClientFactory, ServiceConfiguration serviceConfiguration)
	{
		WalletManager = walletManager;
		HttpClientFactory = backendHttpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		ServiceConfiguration = serviceConfiguration;
	}

	public WalletManager WalletManager { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	public RoundStateUpdater RoundStatusUpdater { get; }
	public ServiceConfiguration ServiceConfiguration { get; }
	private CoinRefrigerator CoinRefrigerator { get; } = new();
	public bool IsUserInSendWorkflow { get; set; }

	public event EventHandler<StatusChangedEventArgs>? StatusChanged;

	public CoinJoinClientState HighestCoinJoinClientState { get; private set; }

	private Channel<CoinJoinCommand> CommandChannel { get; } = Channel.CreateUnbounded<CoinJoinCommand>();

	#region Public API (Start | Stop | )

	public async Task StartAsync(Wallet wallet, bool stopWhenAllMixed, bool overridePlebStop, CancellationToken cancellationToken)
	{
		if (overridePlebStop && !wallet.IsUnderPlebStop)
		{
			// Turn off overriding if we went above the threshold meanwhile.
			overridePlebStop = false;
			wallet.LogDebug($"Do not override PlebStop anymore we are above the threshold.");
		}

		await CommandChannel.Writer.WriteAsync(new StartCoinJoinCommand(wallet, stopWhenAllMixed, overridePlebStop), cancellationToken).ConfigureAwait(false);
	}

	public async Task StopAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await CommandChannel.Writer.WriteAsync(new StopCoinJoinCommand(wallet), cancellationToken).ConfigureAwait(false);
	}

	#endregion Public API (Start | Stop | )

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Detects and notifies about wallets that can participate in a coinjoin.
		var walletsMonitoringTask = Task.Run(() => MonitorWalletsAsync(stoppingToken), stoppingToken);

		// Coinjoin handling Start / Stop and finallization.
		var monitorAndHandleCoinjoinsTask = MonitorAndHandleCoinJoinsAsync(stoppingToken);

		await Task.WhenAny(walletsMonitoringTask, monitorAndHandleCoinjoinsTask).ConfigureAwait(false);

		await WaitAndHandleResultOfTasksAsync(nameof(walletsMonitoringTask), walletsMonitoringTask).ConfigureAwait(false);
		await WaitAndHandleResultOfTasksAsync(nameof(monitorAndHandleCoinjoinsTask), monitorAndHandleCoinjoinsTask).ConfigureAwait(false);
	}

	private async Task MonitorWalletsAsync(CancellationToken stoppingToken)
	{
		var trackedWallets = new Dictionary<string, Wallet>();
		while (!stoppingToken.IsCancellationRequested)
		{
			var mixableWallets = RoundStatusUpdater.AnyRound
				? GetMixableWallets()
				: ImmutableDictionary<string, Wallet>.Empty;

			// Notifies when a wallet meets the criteria for participating in a coinjoin.
			var openedWallets = mixableWallets.Where(x => !trackedWallets.ContainsKey(x.Key)).ToImmutableList();
			foreach (var openedWallet in openedWallets.Select(x => x.Value))
			{
				trackedWallets.Add(openedWallet.WalletName, openedWallet);
				NotifyMixableWalletLoaded(openedWallet);
			}

			// Notifies when a wallet no longer meets the criteria for participating in a coinjoin.
			var closedWallets = trackedWallets.Where(x => !mixableWallets.ContainsKey(x.Key)).ToImmutableList();
			foreach (var closedWallet in closedWallets.Select(x => x.Value))
			{
				//closedWallet.Cancel();
				NotifyMixableWalletUnloaded(closedWallet);
				trackedWallets.Remove(closedWallet.WalletName);
			}
			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
		}
	}

	private async Task MonitorAndHandleCoinJoinsAsync(CancellationToken stoppingToken)
	{
		// This is a shared resource and that's why it is concurrent. Alternatives are locking structures,
		// using a single lock around its access or use a channel.
		var trackedCoinJoins = new ConcurrentDictionary<string, CoinJoinTracker>();
		var trackedAutoStarts = new ConcurrentDictionary<Wallet, TrackedAutoStart>();

		var commandsHandlingTask = Task.Run(() => HandleCoinJoinCommandsAsync(trackedCoinJoins, trackedAutoStarts, stoppingToken), stoppingToken);
		var monitorCoinJoinTask = Task.Run(() => MonitorAndHandlingCoinJoinFinallizationAsync(trackedCoinJoins, trackedAutoStarts, stoppingToken), stoppingToken);

		await Task.WhenAny(commandsHandlingTask, monitorCoinJoinTask).ConfigureAwait(false);

		await WaitAndHandleResultOfTasksAsync(nameof(commandsHandlingTask), commandsHandlingTask).ConfigureAwait(false);
		await WaitAndHandleResultOfTasksAsync(nameof(monitorCoinJoinTask), monitorCoinJoinTask).ConfigureAwait(false);
	}

	private async Task HandleCoinJoinCommandsAsync(ConcurrentDictionary<string, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<Wallet, TrackedAutoStart> trackedAutoStarts, CancellationToken stoppingToken)
	{
		var coinJoinTrackerFactory = new CoinJoinTrackerFactory(HttpClientFactory, RoundStatusUpdater, stoppingToken);

		void StartCoinJoinCommand(StartCoinJoinCommand startCommand)
		{
			var walletToStart = startCommand.Wallet;
			if (trackedCoinJoins.TryGetValue(walletToStart.WalletName, out var tracker))
			{
				if (startCommand.StopWhenAllMixed != tracker.StopWhenAllMixed)
				{
					tracker.StopWhenAllMixed = startCommand.StopWhenAllMixed;

					walletToStart.LogDebug($"Cannot start coinjoin, because it is already running - but updated the value of {nameof(startCommand.StopWhenAllMixed)} to {startCommand.StopWhenAllMixed}.");
				}
				else
				{
					walletToStart.LogDebug("Cannot start coinjoin, because it is already running.");
				}

				return;
			}

			NotifyWalletStartedCoinJoin(walletToStart);

			if (IsUserInSendWorkflow)
			{
				ScheduleRestartAutomatically(walletToStart, trackedAutoStarts, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop, stoppingToken);

				NotifyCoinJoinStartError(walletToStart, CoinjoinError.UserInSendWorkflow);
				return;
			}

			if (walletToStart.IsUnderPlebStop && !startCommand.OverridePlebStop)
			{
				walletToStart.LogDebug("PlebStop preventing coinjoin.");
				ScheduleRestartAutomatically(walletToStart, trackedAutoStarts, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop, stoppingToken);

				NotifyCoinJoinStartError(walletToStart, CoinjoinError.NotEnoughUnprivateBalance);
				return;
			}

			if (WalletManager.Synchronizer?.LastResponse is not { } synchronizerResponse)
			{
				ScheduleRestartAutomatically(walletToStart, trackedAutoStarts, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop, stoppingToken);

				NotifyCoinJoinStartError(walletToStart, CoinjoinError.BackendNotSynchronized);
				return;
			}

			if (IsWalletPrivate(walletToStart))
			{
				walletToStart.LogDebug("All mixed!");
				if (!startCommand.StopWhenAllMixed)
				{
					ScheduleRestartAutomatically(walletToStart, trackedAutoStarts, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop, stoppingToken);
				}

				NotifyCoinJoinStartError(walletToStart, CoinjoinError.AllCoinsPrivate);
				return;
			}

			var coinCandidates = SelectCandidateCoins(walletToStart, synchronizerResponse.BestHeight).ToArray();
			if (coinCandidates.Length == 0)
			{
				walletToStart.LogDebug("No candidate coins available to mix.");
				ScheduleRestartAutomatically(walletToStart, trackedAutoStarts, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop, stoppingToken);

				NotifyCoinJoinStartError(walletToStart, CoinjoinError.NoCoinsToMix);
				return;
			}

			var coinJoinTracker = coinJoinTrackerFactory.CreateAndStart(walletToStart, coinCandidates, startCommand.StopWhenAllMixed, startCommand.OverridePlebStop);

			if (!trackedCoinJoins.TryAdd(walletToStart.WalletName, coinJoinTracker))
			{
				// This should never happen.
				walletToStart.LogError($"{nameof(CoinJoinTracker)} was already added.");
				coinJoinTracker.Stop();
				coinJoinTracker.Dispose();
				return;
			}

			coinJoinTracker.WalletCoinJoinProgressChanged += CoinJoinTracker_WalletCoinJoinProgressChanged;

			var registrationTimeout = TimeSpan.MaxValue;
			NotifyCoinJoinStarted(walletToStart, registrationTimeout);

			walletToStart.LogDebug($"Coinjoin client started, {nameof(startCommand.StopWhenAllMixed)}:'{startCommand.StopWhenAllMixed}' {nameof(startCommand.OverridePlebStop)}:'{startCommand.OverridePlebStop}'.");

			// In case there was another start scheduled just remove it.
			TryRemoveTrackedAutoStart(walletToStart);
		}

		void StopCoinJoinCommand(StopCoinJoinCommand stopCommand)
		{
			var walletToStop = stopCommand.Wallet;

			var autoStartRemoved = TryRemoveTrackedAutoStart(walletToStop);

			if (trackedCoinJoins.TryGetValue(walletToStop.WalletName, out var coinJoinTrackerToStop))
			{
				coinJoinTrackerToStop.Stop();
				if (coinJoinTrackerToStop.InCriticalCoinJoinState)
				{
					walletToStop.LogWarning($"Coinjoin is in critical phase, it cannot be stopped - it won't restart later.");
				}
			}
			else if (autoStartRemoved)
			{
				NotifyWalletStoppedCoinJoin(walletToStop);
			}
		}

		bool TryRemoveTrackedAutoStart(Wallet wallet)
		{
			if (trackedAutoStarts.TryRemove(wallet, out var trackedAutoStart))
			{
				trackedAutoStart.CancellationTokenSource.Cancel();
				trackedAutoStart.CancellationTokenSource.Dispose();
				return true;
			}
			return false;
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			var command = await CommandChannel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);

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

	private void ScheduleRestartAutomatically(Wallet walletToStart, ConcurrentDictionary<Wallet, TrackedAutoStart> trackedAutoStarts, bool stopWhenAllMixed, bool overridePlebStop, CancellationToken stoppingToken)
	{
		if (trackedAutoStarts.ContainsKey(walletToStart))
		{
			walletToStart.LogDebug($"AutoStart was already scheduled.");
			return;
		}

#pragma warning disable CA2000 // Dispose objects before losing scope
		var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
#pragma warning restore CA2000 // Dispose objects before losing scope

		var restartTask = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(30), linkedCts.Token).ConfigureAwait(false);
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
				await StartAsync(walletToStart, stopWhenAllMixed, overridePlebStop, stoppingToken).ConfigureAwait(false);
			}
			else
			{
				walletToStart.LogInfo($"AutoStart was already handled.");
			}
		}, linkedCts.Token);

		if (!trackedAutoStarts.TryAdd(walletToStart, new TrackedAutoStart(restartTask, linkedCts)))
		{
			walletToStart.LogInfo($"AutoCoinJoin task was already added.");
		}
	}

	private async Task MonitorAndHandlingCoinJoinFinallizationAsync(ConcurrentDictionary<string, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<Wallet, TrackedAutoStart> trackedAutoStarts, CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			// Handles coinjoin finalization and notification.
			var finishedCoinJoins = trackedCoinJoins.Where(x => x.Value.IsCompleted).Select(x => x.Value).ToImmutableArray();
			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				await HandleCoinJoinFinalizationAsync(finishedCoinJoin, trackedCoinJoins, stoppingToken).ConfigureAwait(false);

				NotifyCoinJoinCompletion(finishedCoinJoin);

				if (!finishedCoinJoin.IsStopped && !stoppingToken.IsCancellationRequested)
				{
					finishedCoinJoin.Wallet.LogInfo($"{nameof(CoinJoinClient)} restart automatically.");

					ScheduleRestartAutomatically(finishedCoinJoin.Wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, stoppingToken);
				}
				else
				{
					NotifyWalletStoppedCoinJoin(finishedCoinJoin.Wallet);
				}
			}
			// Updates the highest coinjoin client state.
			var onGoingCoinJoins = trackedCoinJoins.Values.Where(wtd => !wtd.IsCompleted).ToImmutableArray();
			var scheduledCoinJoins = trackedAutoStarts.Select(t => t.Key);

			var onGoingHighestState = onGoingCoinJoins.IsEmpty
				? CoinJoinClientState.Idle
				: onGoingCoinJoins.Any(wtd => wtd.InCriticalCoinJoinState)
					? CoinJoinClientState.InCriticalPhase
					: CoinJoinClientState.InProgress;

			HighestCoinJoinClientState = onGoingHighestState is not CoinJoinClientState.Idle
				? onGoingHighestState
				: scheduledCoinJoins.Any()
					? CoinJoinClientState.InProgress
					: CoinJoinClientState.Idle;

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
		}
	}

	private async Task HandleCoinJoinFinalizationAsync(CoinJoinTracker finishedCoinJoin, ConcurrentDictionary<string, CoinJoinTracker> trackedCoinJoins, CancellationToken cancellationToken)
	{
		var wallet = finishedCoinJoin.Wallet;

		try
		{
			var result = await finishedCoinJoin.CoinJoinTask.ConfigureAwait(false);
			if (result.SuccessfulBroadcast)
			{
				CoinRefrigerator.Freeze(result.RegisteredCoins);
				MarkDestinationsUsed(result.RegisteredOutputs);
				wallet.LogInfo($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was broadcast.");
			}
			else
			{
				wallet.LogInfo($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was not broadcast.");
			}
		}
		catch (NoCoinsToMixException x)
		{
			NotifyCoinJoinStartError(wallet, CoinjoinError.NoCoinsToMix);
			Logger.LogDebug(x);
		}
		catch (InvalidOperationException ioe)
		{
			Logger.LogWarning(ioe);
		}
		catch (OperationCanceledException)
		{
			if (finishedCoinJoin.IsStopped)
			{
				wallet.LogInfo($"{nameof(CoinJoinClient)} was stopped.");
			}
			else
			{
				wallet.LogInfo($"{nameof(CoinJoinClient)} was cancelled.");
			}
		}
		catch (UnexpectedRoundPhaseException e)
		{
			// `UnexpectedRoundPhaseException` indicates an error in the protocol however,
			// temporarily we are shortening the circuit by aborting the rounds if 
			// there are Alices that didn't confirm.
			// The fix is already done but the clients have to upgrade.
			wallet.LogInfo($"{nameof(CoinJoinClient)} failed with exception: '{e}'");
		}
		catch (Exception e)
		{
			wallet.LogError($"{nameof(CoinJoinClient)} failed with exception: '{e}'");
		}

		if (!trackedCoinJoins.TryRemove(wallet.WalletName, out _))
		{
			wallet.LogWarning($"Was not removed from tracked wallet list. Will retry in a few seconds.");
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
	private void MarkDestinationsUsed(ImmutableList<Script> outputs)
	{
		var hashSet = outputs.ToHashSet();

		foreach (var k in WalletManager
			.GetWallets(false)
			.Select(w => w.KeyManager)
			.SelectMany(k => k.GetKeys(k => hashSet.Contains(k.P2wpkhScript))))
		{
			k.SetKeyState(KeyState.Used);
		}
	}

	private void NotifyWalletStartedCoinJoin(Wallet openedWallet) =>
		StatusChanged.SafeInvoke(this, new WalletStartedCoinJoinEventArgs(openedWallet));

	private void NotifyWalletStoppedCoinJoin(Wallet openedWallet) =>
	StatusChanged.SafeInvoke(this, new WalletStoppedCoinJoinEventArgs(openedWallet));

	private void NotifyCoinJoinStarted(Wallet openedWallet, TimeSpan registrationTimeout) =>
		StatusChanged.SafeInvoke(this, new StartedEventArgs(openedWallet, registrationTimeout));

	private void NotifyCoinJoinStartError(Wallet openedWallet, CoinjoinError error) =>
		StatusChanged.SafeInvoke(this, new StartErrorEventArgs(openedWallet, error));

	private void NotifyMixableWalletUnloaded(Wallet closedWallet) =>
		StatusChanged.SafeInvoke(this, new StoppedEventArgs(closedWallet, StopReason.WalletUnloaded));

	private void NotifyMixableWalletLoaded(Wallet openedWallet) =>
		StatusChanged.SafeInvoke(this, new LoadedEventArgs(openedWallet));

	private void NotifyCoinJoinCompletion(CoinJoinTracker finishedCoinJoin) =>
		StatusChanged.SafeInvoke(this, new CompletedEventArgs(
			finishedCoinJoin.Wallet,
			finishedCoinJoin.CoinJoinTask.Status switch
			{
				TaskStatus.RanToCompletion when finishedCoinJoin.CoinJoinTask.Result.SuccessfulBroadcast => CompletionStatus.Success,
				TaskStatus.Canceled => CompletionStatus.Canceled,
				TaskStatus.Faulted => CompletionStatus.Failed,
				_ => CompletionStatus.Unknown,
			}));

	private void NotifyCoinJoinStatusChanged(Wallet wallet, CoinJoinProgressEventArgs coinJoinProgressEventArgs) =>
		StatusChanged.SafeInvoke(this, new CoinJoinStatusEventArgs(
			wallet,
			coinJoinProgressEventArgs));

	private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
		WalletManager.GetWallets()
			.Where(x => x.State == WalletState.Started // Only running wallets
					&& !x.KeyManager.IsWatchOnly // that are not watch-only wallets
					&& x.Kitchen.HasIngredients)
			.ToImmutableDictionary(x => x.WalletName, x => x);

	private bool IsWalletPrivate(Wallet wallet)
	{
		var coins = new CoinsView(wallet.Coins);

		if (GetPrivacyPercentage(coins, wallet.KeyManager.AnonScoreTarget) >= 1)
		{
			return true;
		}

		return false;
	}

	private IEnumerable<SmartCoin> SelectCandidateCoins(Wallet openedWallet, int bestHeight)
	{
		var coins = new CoinsView(openedWallet.Coins
			.Available()
			.Confirmed()
			.Where(x => !x.IsImmature(bestHeight))
			.Where(x => !x.IsBanned)
			.Where(x => !CoinRefrigerator.IsFrozen(x)));

		return coins;
	}

	private double GetPrivacyPercentage(CoinsView coins, int privateThreshold)
	{
		var privateAmount = coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		var normalAmount = coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();

		var privateDecimalAmount = privateAmount.ToDecimal(MoneyUnit.BTC);
		var normalDecimalAmount = normalAmount.ToDecimal(MoneyUnit.BTC);
		var totalDecimalAmount = privateDecimalAmount + normalDecimalAmount;

		var pcPrivate = totalDecimalAmount == 0M ? 1d : (double)(privateDecimalAmount / totalDecimalAmount);
		return pcPrivate;
	}

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

	private void CoinJoinTracker_WalletCoinJoinProgressChanged(object? sender, CoinJoinProgressEventArgs e)
	{
		if (sender is not Wallet wallet)
		{
			throw new InvalidOperationException("Sender must be a wallet.");
		}

		NotifyCoinJoinStatusChanged(wallet, e);
	}

	private record TrackedAutoStart(Task Task, CancellationTokenSource CancellationTokenSource);
}
