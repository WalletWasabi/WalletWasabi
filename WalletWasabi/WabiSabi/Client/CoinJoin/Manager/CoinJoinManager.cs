using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.WalletCoinJoin;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.WabiSabi.Client.CoinJoin.WalletCoinJoin.WalletCoinJoinClient;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinManager : BackgroundService
{
	public CoinJoinManager(IWalletProvider walletProvider, RoundStateUpdater roundStatusUpdater, IWasabiHttpClientFactory coordinatorHttpClientFactory, IWasabiBackendStatusProvider wasabiBackendStatusProvider, string coordinatorIdentifier, CoinPrison coinPrison)
	{
		WasabiBackendStatusProvide = wasabiBackendStatusProvider;
		WalletProvider = walletProvider;
		HttpClientFactory = coordinatorHttpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		CoordinatorIdentifier = coordinatorIdentifier;
		CoinPrison = coinPrison;
	}

	public event EventHandler<StatusChangedEventArgs>? StatusChanged;

	private IWasabiBackendStatusProvider WasabiBackendStatusProvide { get; }

	public ImmutableDictionary<WalletId, ImmutableList<SmartCoin>> CoinsInCriticalPhase { get; set; } = ImmutableDictionary<WalletId, ImmutableList<SmartCoin>>.Empty;
	public IWalletProvider WalletProvider { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	public RoundStateUpdater RoundStatusUpdater { get; }
	public string CoordinatorIdentifier { get; }
	public CoinPrison CoinPrison { get; }
	private CoinRefrigerator CoinRefrigerator { get; } = new();

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

	#region Public API (Start | Stop | TryGetWalletStatus)

	public async Task StartAsync(IWallet wallet, IWallet outputWallet, bool stopWhenAllMixed, bool overridePlebStop, CancellationToken cancellationToken)
	{
		if (!WalletCoinJoinClients.TryGetValue(wallet.WalletId, out var walletCoinJoinClient))
		{
			Logger.LogError("Problem");
			return;
		}

		await walletCoinJoinClient.StartAsync(wallet, outputWallet, stopWhenAllMixed, overridePlebStop, cancellationToken).ConfigureAwait(false);
	}

	public async Task StopAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		if (!WalletCoinJoinClients.TryGetValue(wallet.WalletId, out var walletCoinJoinClient))
		{
			Logger.LogError("Problem");
			return;
		}

		await walletCoinJoinClient.StopAsync(wallet, cancellationToken).ConfigureAwait(false);
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

	private Dictionary<WalletId, WalletCoinJoinClient> WalletCoinJoinClients { get; } = [];

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		foreach (var wallet in await WalletProvider.GetWalletsAsync().ConfigureAwait(false))
		{
			// TODO: make sure, wallets generated later will be added too.
			WalletCoinJoinClients.Add(wallet.WalletId, new WalletCoinJoinClient(
				wallet,
				new CoinJoinTrackerFactory(HttpClientFactory, RoundStatusUpdater, CoordinatorIdentifier, stoppingToken),
				RoundStatusUpdater,
				CoinRefrigerator,
				CoinPrison,
				WasabiBackendStatusProvide));
		}

		while (WasabiBackendStatusProvide.LastResponse is not { } synchronizerResponse)
		{
			await Task.Delay(10000, stoppingToken).ConfigureAwait(false);
		}

		foreach (var walletCoinJoinClient in WalletCoinJoinClients.Values)
		{
			await walletCoinJoinClient.StartAsync(stoppingToken).ConfigureAwait(false);
		}

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
			var mixableWallets = RoundStatusUpdater.AnyRound
				? await GetMixableWalletsAsync().ConfigureAwait(false)
				: ImmutableDictionary<WalletId, IWallet>.Empty;

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

		var monitorCoinJoinTask = Task.Run(() => MonitorAndHandlingCoinJoinFinalizationAsync(trackedCoinJoins, trackedAutoStarts, stoppingToken), stoppingToken);

		await Task.WhenAny(monitorCoinJoinTask).ConfigureAwait(false);

		await WaitAndHandleResultOfTasksAsync(nameof(monitorCoinJoinTask), monitorCoinJoinTask).ConfigureAwait(false);
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
				walletToStart.LogDebug("AutoStart was already scheduled");
				return;
			}

			walletToStart.LogDebug("AutoStart was already scheduled with different parameters, cancel the last task and do not wait.");
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
					walletToStart.LogInfo("AutoStart was already handled.");
				}
			},
			linkedCts.Token);

		if (trackedAutoStarts.TryAdd(walletToStart, new TrackedAutoStart(restartTask, stopWhenAllMixed, overridePlebStop, outputWallet, linkedCts)))
		{
			restartTask.Start();
		}
		else
		{
			walletToStart.LogInfo("AutoCoinJoin task was already added.");
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
			var wallets = await WalletProvider.GetWalletsAsync().ConfigureAwait(false);

			CoinJoinClientStates = GetCoinJoinClientStates(wallets, trackedCoinJoins, trackedAutoStarts);
			CoinsInCriticalPhase = GetCoinsInCriticalPhase(wallets, trackedCoinJoins);
			RoundStatusUpdater.SlowRequestsMode = HighestCoinJoinClientState is CoinJoinClientState.Idle;

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
		(await WalletProvider.GetWalletsAsync().ConfigureAwait(false))
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
			Logger.LogDebug("Wallet was not in send workflow but left it.");
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
			Logger.LogDebug("Wallet tried to enter sending but it was not in the send workflow.");
			return;
		}

		if (!CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug("Wallet tried to enter sending but state was missing.");
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
		foreach (var wallet in await WalletProvider.GetWalletsAsync().ConfigureAwait(false))
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
		foreach (var wallet in await WalletProvider.GetWalletsAsync().ConfigureAwait(false))
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

	private record TrackedAutoStart(Task Task, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet, CancellationTokenSource CancellationTokenSource);
	private record CoinJoinClientStateHolder(CoinJoinClientState CoinJoinClientState, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet);
	private record UiBlockedStateHolder(bool NeedRestart, bool StopWhenAllMixed, bool OverridePlebStop, IWallet OutputWallet);
}
