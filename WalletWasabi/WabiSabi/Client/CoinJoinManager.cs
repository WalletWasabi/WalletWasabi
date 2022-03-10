using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

internal enum CoinJoinCommand
{
	Start,
	Stop,
}

public class CoinJoinManager : BackgroundService
{
	public CoinJoinManager(WalletManager walletManager, RoundStateUpdater roundStatusUpdater, IWasabiHttpClientFactory backendHttpClientFactory, ServiceConfiguration serviceConfiguration)
	{
		WalletManager = walletManager;
		HttpClientFactory = backendHttpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		ServiceConfiguration = serviceConfiguration;
	}

	public event EventHandler<WalletStatusChangedEventArgs>? WalletStatusChanged;

	public WalletManager WalletManager { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	public RoundStateUpdater RoundStatusUpdater { get; }
	public ServiceConfiguration ServiceConfiguration { get; }
	private ImmutableDictionary<string, CoinJoinTracker> TrackedCoinJoins { get; set; } = ImmutableDictionary<string, CoinJoinTracker>.Empty;
	private CoinRefrigerator CoinRefrigerator { get; } = new();
	public bool IsUserInSendWorkflow { get; set; }

	private ConcurrentDictionary<Wallet, CoinJoinCommand> WalletManualState { get; } = new();

	public void Start(Wallet wallet) =>
		WalletManualState.AddOrUpdate(wallet, CoinJoinCommand.Start, (_, _) => CoinJoinCommand.Start);

	public void Stop(Wallet wallet) =>
		WalletManualState.AddOrUpdate(wallet, CoinJoinCommand.Stop, (_, _) => CoinJoinCommand.Stop);

	public event EventHandler<StatusChangedEventArgs>? StatusChanged;

	public CoinJoinClientState HighestCoinJoinClientState
	{
		get
		{
			var inProgress = TrackedCoinJoins.Values.Where(wtd => !wtd.IsCompleted).ToImmutableArray();

			if (inProgress.IsEmpty)
			{
				return CoinJoinClientState.Idle;
			}

			return inProgress.Any(wtd => wtd.InCriticalCoinJoinState)
				? CoinJoinClientState.InCriticalPhase
				: CoinJoinClientState.InProgress;
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (WalletManager.Network == Network.Main)
		{
			Logger.LogInfo("WabiSabi coinjoin client-side functionality is disabled temporarily on mainnet.");
			return;
		}
		CoinJoinTrackerFactory coinJoinTrackerFactory = new(HttpClientFactory, RoundStatusUpdater, stoppingToken);

		var trackedCoinJoins = new Dictionary<string, CoinJoinTracker>();

		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);

			var mixableWallets = RoundStatusUpdater.AnyRound
				? GetMixableWallets()
				: ImmutableDictionary<string, Wallet>.Empty;
			var openedWallets = mixableWallets.Where(x => !trackedCoinJoins.ContainsKey(x.Key)).ToImmutableList();
			var closedWallets = trackedCoinJoins.Where(x => !mixableWallets.ContainsKey(x.Key)).ToImmutableList();

			foreach (var openedWallet in openedWallets.Select(x => x.Value))
			{
				NotifyMixableWalletLoaded(openedWallet);

				if (!MustStart(openedWallet))
				{
					continue;
				}

				if (openedWallet.KeyManager.AutoCoinJoin)
				{
					if (IsUserInSendWorkflow)
					{
						NotifyCoinJoinStartError(openedWallet, CoinjoinError.UserInSendWorkflow);
						continue;
					}
					if (openedWallet.NonPrivateCoins.TotalAmount() <= openedWallet.KeyManager.PlebStopThreshold)
					{
						NotifyCoinJoinStartError(openedWallet, CoinjoinError.NotEnoughUnprivateBalance);
						continue;
					}
				}

				var coinCandidates = SelectCandidateCoins(openedWallet).ToArray();
				if (coinCandidates.Length == 0)
				{
					NotifyCoinJoinStartError(openedWallet, CoinjoinError.NoCoinsToMix);
					continue;
				}

				CoinJoinTracker coinJoinTracker = coinJoinTrackerFactory.CreateAndStart(openedWallet, coinCandidates);

				trackedCoinJoins.Add(openedWallet.WalletName, coinJoinTracker);
				var registrationTimeout = TimeSpan.MaxValue;
				NotifyCoinJoinStarted(openedWallet, registrationTimeout);
				WalletStatusChanged?.Invoke(this, new WalletStatusChangedEventArgs(openedWallet, IsCoinJoining: true));
			}

			foreach (var closedWallet in closedWallets.Select(x => x.Value))
			{
				closedWallet.Cancel();
				NotifyMixableWalletUnloaded(closedWallet);
			}

			var finishedCoinJoins = trackedCoinJoins
				.Where(x => x.Value.IsCompleted)
				.Select(x => x.Value)
				.ToImmutableArray();

			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				NotifyCoinJoinCompletion(finishedCoinJoin);

				var walletToRemove = finishedCoinJoin.Wallet;
				if (!trackedCoinJoins.Remove(walletToRemove.WalletName))
				{
					Logger.LogWarning($"Wallet: `{walletToRemove.WalletName}` was not removed from tracked wallet list. Will retry in a few seconds.");
				}
				else
				{
					WalletStatusChanged?.Invoke(this, new WalletStatusChangedEventArgs(walletToRemove, IsCoinJoining: false));
					finishedCoinJoin.Dispose();
				}
			}

			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
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
					await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					Logger.LogInfo($"{logPrefix} was cancelled.");
				}
				catch (Exception e)
				{
					Logger.LogError($"{logPrefix} failed with exception:", e);
				}

				foreach (var coins in finishedCoinJoin.CoinCandidates)
				{
					coins.CoinJoinInProgress = false;
				}
			}

			TrackedCoinJoins = trackedCoinJoins.ToImmutableDictionary();
		}
	}

	/// <summary>
	/// Mark all the registered outputs we had in any of our wallets used.
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

	private void NotifyMixableWalletUnloaded(CoinJoinTracker closedWallet) =>
		SafeRaiseEvent(StatusChanged, new StoppedEventArgs(closedWallet.Wallet, StopReason.WalletUnloaded));

	private void NotifyMixableWalletLoaded(Wallet openedWallet) =>
		SafeRaiseEvent(StatusChanged, new LoadedEventArgs(openedWallet));

	private void NotifyCoinJoinCompletion(CoinJoinTracker finishedCoinJoin) =>
		SafeRaiseEvent(StatusChanged, new CoinJoinCompletedEventArgs(
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
			.Where(x => x.State == WalletState.Started) // Only running wallets
			.Where(x => !x.KeyManager.IsWatchOnly)      // that are not watch-only wallets
			.Where(x => x.Kitchen.HasIngredients)
			.Where(x => x.KeyManager.AutoCoinJoin || MustStart(x))
			.Where(x => !MustStop(x))
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

	private bool MustStart(Wallet wallet) =>
		WalletManualState.TryGetValue(wallet, out var state) && state == CoinJoinCommand.Start;

	private bool MustStop(Wallet wallet) =>
		WalletManualState.TryGetValue(wallet, out var state) && state == CoinJoinCommand.Stop;

	private IEnumerable<SmartCoin> SelectCandidateCoins(Wallet openedWallet)
	{
		var coins = new CoinsView(openedWallet.Coins
			.Available()
			.Confirmed()
			.Where(x => x.HdPubKey.AnonymitySet < openedWallet.KeyManager.MaxAnonScoreTarget)
			.Where(x => !CoinRefrigerator.IsFrozen(x)));

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
}
