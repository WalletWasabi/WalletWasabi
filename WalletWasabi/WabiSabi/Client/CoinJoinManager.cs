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
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinManager : BackgroundService
{
	private record CoinJoinCommand(Wallet Wallet);
	private record StartCoinJoinCommand(Wallet Wallet, bool RestartAutomatically) : CoinJoinCommand(Wallet);
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

	#region Public API (Start | Stop | StartAutomatically)

	public async Task StartAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await CommandChannel.Writer.WriteAsync(new StartCoinJoinCommand(wallet, false), cancellationToken).ConfigureAwait(false);
	}

	public async Task StartAutomaticallyAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await CommandChannel.Writer.WriteAsync(new StartCoinJoinCommand(wallet, true), cancellationToken).ConfigureAwait(false);
	}

	public async Task StopAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await CommandChannel.Writer.WriteAsync(new StopCoinJoinCommand(wallet), cancellationToken).ConfigureAwait(false);
	}

	#endregion Public API (Start | Stop | StartAutomatically)

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (WalletManager.Network == Network.Main)
		{
			Logger.LogInfo("WabiSabi coinjoin client-side functionality is disabled temporarily on mainnet.");
			return;
		}

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

		var commandsHandlingTask = Task.Run(() => HandleCoinJoinCommandsAsync(trackedCoinJoins, stoppingToken), stoppingToken);
		var monitorCoinJoinTask = Task.Run(() => MonitorAndHandlingCoinJoinFinallizationAsync(trackedCoinJoins, stoppingToken), stoppingToken);

		await Task.WhenAny(commandsHandlingTask, monitorCoinJoinTask).ConfigureAwait(false);

		await WaitAndHandleResultOfTasksAsync(nameof(commandsHandlingTask), commandsHandlingTask).ConfigureAwait(false);
		await WaitAndHandleResultOfTasksAsync(nameof(monitorCoinJoinTask), monitorCoinJoinTask).ConfigureAwait(false);
	}

	private async Task HandleCoinJoinCommandsAsync(ConcurrentDictionary<string, CoinJoinTracker> trackedCoinJoins, CancellationToken stoppingToken)
	{
		var coinJoinTrackerFactory = new CoinJoinTrackerFactory(HttpClientFactory, RoundStatusUpdater, stoppingToken);
		var trackedAutoStarts = new ConcurrentDictionary<Wallet, Task>();

		void StartCoinJoinCommand(StartCoinJoinCommand startCommand)
		{
			var walletToStart = startCommand.Wallet;
			if (trackedCoinJoins.TryGetValue(walletToStart.WalletName, out var tracker) && !tracker.IsCompleted)
			{
				Logger.LogDebug($"Cannot start coinjoin for wallet '{walletToStart.WalletName}', bacause it is already running .");
				return;
			}

			// Only take PlebStop into account when AutoCoinJoin.
			if (startCommand.RestartAutomatically && walletToStart.NonPrivateCoins.TotalAmount() <= walletToStart.KeyManager.PlebStopThreshold)
			{
				Logger.LogDebug($"PlebStop preventing coinjoin for wallet '{walletToStart.WalletName}'.");
				NotifyCoinJoinStartError(walletToStart, CoinjoinError.NotEnoughUnprivateBalance);
				ScheduleRestartAutomatically(walletToStart);
				return;
			}

			var coinCandidates = SelectCandidateCoins(walletToStart).ToArray();
			if (coinCandidates.Length == 0)
			{
				Logger.LogDebug($"No Coins to mix for wallet '{walletToStart.WalletName}'.");
				NotifyCoinJoinStartError(walletToStart, CoinjoinError.NoCoinsToMix);
				if (startCommand.RestartAutomatically)
				{
					ScheduleRestartAutomatically(walletToStart);
				}
				return;
			}

			var coinJoinTracker = coinJoinTrackerFactory.CreateAndStart(walletToStart, coinCandidates, startCommand.RestartAutomatically);

			trackedCoinJoins.AddOrUpdate(walletToStart.WalletName, _ => coinJoinTracker, (_, cjt) => cjt);
			var registrationTimeout = TimeSpan.MaxValue;
			NotifyCoinJoinStarted(walletToStart, registrationTimeout);
			Logger.LogDebug($"Coinjoin client started for wallet '{walletToStart.WalletName}'.");
		}

		void StopCoinJoinCommand(StopCoinJoinCommand stopCommand)
		{
			var walletToStop = stopCommand.Wallet;
			if (trackedCoinJoins.TryGetValue(walletToStop.WalletName, out var coinJoinTrackerToStop))
			{
				coinJoinTrackerToStop.Stop();
			}
		}

		void ScheduleRestartAutomatically(Wallet walletToStart)
		{
			if (trackedAutoStarts.ContainsKey(walletToStart))
			{
				Logger.LogDebug($"AutoStart was already scheduled for wallet '{walletToStart.WalletName}'.");
				return;
			}

			var restartTask = Task.Run(async () =>
			{
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
				if (trackedAutoStarts.TryRemove(walletToStart, out _))
				{
					await StartAutomaticallyAsync(walletToStart, stoppingToken).ConfigureAwait(false);
				}
				else
				{
					Logger.LogInfo($"AutoStart was already handled for wallet '{walletToStart.WalletName}'.");
				}
			}, stoppingToken);

			if (!trackedAutoStarts.TryAdd(walletToStart, restartTask))
			{
				Logger.LogInfo($"AutoCoinJoin task was already added for wallet: '{walletToStart.WalletName}'.");
			}
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

		await WaitAndHandleResultOfTasksAsync(nameof(trackedAutoStarts), trackedAutoStarts.Values.ToArray()).ConfigureAwait(false);
	}

	private async Task MonitorAndHandlingCoinJoinFinallizationAsync(ConcurrentDictionary<string, CoinJoinTracker> trackedCoinJoins, CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			// Handles coinjoin finalization and notification.
			var finishedCoinJoins = trackedCoinJoins.Where(x => x.Value.IsCompleted).Select(x => x.Value).ToImmutableArray();
			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				NotifyCoinJoinCompletion(finishedCoinJoin);
				await HandleCoinJoinFinalizationAsync(finishedCoinJoin, trackedCoinJoins, stoppingToken).ConfigureAwait(false);
			}
			// Updates the highest coinjoin client state.
			var inProgress = trackedCoinJoins.Values.Where(wtd => !wtd.IsCompleted).ToImmutableArray();

			HighestCoinJoinClientState = inProgress.IsEmpty
				? CoinJoinClientState.Idle
				: inProgress.Any(wtd => wtd.InCriticalCoinJoinState)
					? CoinJoinClientState.InCriticalPhase
					: CoinJoinClientState.InProgress;

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
		}
	}

	private async Task HandleCoinJoinFinalizationAsync(CoinJoinTracker finishedCoinJoin, ConcurrentDictionary<string, CoinJoinTracker> trackedCoinJoins, CancellationToken cancellationToken)
	{
		var walletToRemove = finishedCoinJoin.Wallet;
		if (!trackedCoinJoins.TryRemove(walletToRemove.WalletName, out _))
		{
			Logger.LogWarning($"Wallet: `{walletToRemove.WalletName}` was not removed from tracked wallet list. Will retry in a few seconds.");
		}
		else
		{
			finishedCoinJoin.Dispose();
		}

		var logPrefix = $"Wallet: `{finishedCoinJoin.Wallet.WalletName}` - Coinjoin client";

		try
		{
			var result = await finishedCoinJoin.CoinJoinTask.ConfigureAwait(false);
			if (result.SuccessfulBroadcast)
			{
				CoinRefrigerator.Freeze(result.RegisteredCoins);
				MarkDestinationsUsed(result.RegisteredOutputs);
				Logger.LogInfo($"{logPrefix} finished!");
			}
			else
			{
				Logger.LogInfo($"{logPrefix} finished with error. Transaction not broadcasted.");
			}
		}
		catch (InvalidOperationException ioe)
		{
			Logger.LogError(ioe);
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			if (finishedCoinJoin.IsStopped)
			{
				Logger.LogInfo($"{logPrefix} was stopped.");
			}
			else
			{
				Logger.LogInfo($"{logPrefix} was cancelled.");
			}
		}
		catch (Exception e)
		{
			Logger.LogError($"{logPrefix} failed with exception:", e);
		}

		foreach (var coins in finishedCoinJoin.CoinCandidates)
		{
			coins.CoinJoinInProgress = false;
		}

		if (finishedCoinJoin.RestartAutomatically &&
			!finishedCoinJoin.IsStopped &&
			!cancellationToken.IsCancellationRequested)
		{
			Logger.LogInfo($"{logPrefix} restart automatically.");

			await StartAutomaticallyAsync(finishedCoinJoin.Wallet, cancellationToken).ConfigureAwait(false);
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

	private void NotifyCoinJoinStarted(Wallet openedWallet, TimeSpan registrationTimeout) =>
		SafeRaiseEvent(StatusChanged, new StartedEventArgs(openedWallet, registrationTimeout));

	private void NotifyCoinJoinStartError(Wallet openedWallet, CoinjoinError error) =>
		SafeRaiseEvent(StatusChanged, new StartErrorEventArgs(openedWallet, error));

	private void NotifyMixableWalletUnloaded(Wallet closedWallet) =>
		SafeRaiseEvent(StatusChanged, new StoppedEventArgs(closedWallet, StopReason.WalletUnloaded));

	private void NotifyMixableWalletLoaded(Wallet openedWallet) =>
		SafeRaiseEvent(StatusChanged, new LoadedEventArgs(openedWallet));

	private void NotifyCoinJoinCompletion(CoinJoinTracker finishedCoinJoin) =>
		SafeRaiseEvent(StatusChanged, new CompletedEventArgs(
			finishedCoinJoin.Wallet,
			finishedCoinJoin.CoinJoinTask.Status switch
			{
				TaskStatus.RanToCompletion when finishedCoinJoin.CoinJoinTask.Result.SuccessfulBroadcast => CompletionStatus.Success,
				TaskStatus.Canceled => CompletionStatus.Canceled,
				TaskStatus.Faulted => CompletionStatus.Failed,
				_ => CompletionStatus.Unknown,
			}));

	private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
		WalletManager.GetWallets()
			.Where(x => x.State == WalletState.Started // Only running wallets
					&& !x.KeyManager.IsWatchOnly // that are not watch-only wallets
					&& x.Kitchen.HasIngredients)
			.ToImmutableDictionary(x => x.WalletName, x => x);

	private void SafeRaiseEvent(EventHandler<StatusChangedEventArgs>? evnt, StatusChangedEventArgs args)
	{
		try
		{
			evnt?.Invoke(this, args);
		}
		catch (Exception e)
		{
			Logger.LogError(e);
		}
	}

	private IEnumerable<SmartCoin> SelectCandidateCoins(Wallet openedWallet)
	{
		var coins = new CoinsView(openedWallet.Coins
			.Available()
			.Confirmed()
			.Where(x => x.HdPubKey.AnonymitySet < openedWallet.KeyManager.MaxAnonScoreTarget
					&& !CoinRefrigerator.IsFrozen(x)));

		// If a small portion of the wallet isn't private, it's better to wait with mixing.
		if (GetPrivacyPercentage(coins, openedWallet.KeyManager.MinAnonScoreTarget) > 0.99)
		{
			return Enumerable.Empty<SmartCoin>();
		}

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
}
