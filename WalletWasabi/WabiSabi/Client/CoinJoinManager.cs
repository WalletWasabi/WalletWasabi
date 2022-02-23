using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinManager : BackgroundService
{
	public CoinJoinManager(WalletManager walletManager, RoundStateUpdater roundStatusUpdater, IWasabiHttpClientFactory backendHttpClientFactory, ServiceConfiguration serviceConfiguration)
	{
		WalletManager = walletManager;
		HttpClientFactory = backendHttpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		ServiceConfiguration = serviceConfiguration;
		walletManager.WalletAdded += WalletManager_WalletAdded;
		WalletCoinJoinStates = walletManager.GetWallets().ToDictionary(w => w.WalletName, w => new WalletCoinJoinState(w));
	}

	private void WalletManager_WalletAdded(object? sender, Wallet w)
	{
		WalletCoinJoinStates.Add(w.WalletName, new WalletCoinJoinState(w));
	}

	public WalletManager WalletManager { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	public RoundStateUpdater RoundStatusUpdater { get; }
	public ServiceConfiguration ServiceConfiguration { get; }
	public Dictionary<string, WalletCoinJoinState> WalletCoinJoinStates { get; private set; }
	private CoinRefrigerator CoinRefrigerator { get; } = new();

	public CoinJoinClientState HighestCoinJoinClientState => WalletCoinJoinStates.Values.Select(w => w.CoinJoinClientState).MaxBy(s => (int)s);

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
			var openedWallets = mixableWallets.Where(x => !trackedCoinJoins.ContainsKey(x.Key));
			var closedWallets = trackedCoinJoins.Where(x => !mixableWallets.ContainsKey(x.Key));

			foreach (var openedWallet in openedWallets.Select(x => x.Value))
			{
				var coinCandidates = SelectCandidateCoins(openedWallet).ToArray();
				if (coinCandidates.Length == 0)
				{
					continue;
				}

				CoinJoinTracker coinJoinTracker = coinJoinTrackerFactory.CreateAndStart(openedWallet, coinCandidates);

				trackedCoinJoins.Add(openedWallet.WalletName, coinJoinTracker);
				WalletCoinJoinStates[openedWallet.WalletName].SetCoinJoinTracker(coinJoinTracker);
			}

			foreach (var closedWallet in closedWallets.Select(x => x.Value))
			{
				closedWallet.Cancel();
			}

			var finishedCoinJoins = trackedCoinJoins
				.Where(x => x.Value.IsCompleted)
				.Select(x => x.Value)
				.ToImmutableArray();

			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				var walletToRemove = finishedCoinJoin.Wallet;
				if (!trackedCoinJoins.Remove(walletToRemove.WalletName))
				{
					Logger.LogWarning($"Wallet: `{walletToRemove.WalletName}` was not removed from tracked wallet list. Will retry in a few seconds.");
				}
				else
				{
					WalletCoinJoinStates[walletToRemove.WalletName].ClearCoinJoinTracker();
					finishedCoinJoin.Dispose();
				}
			}

			foreach (var finishedCoinJoin in finishedCoinJoins)
			{
				var logPrefix = $"Wallet: `{finishedCoinJoin.Wallet.WalletName}` - Coinjoin client";

				try
				{
					var success = await finishedCoinJoin.CoinJoinTask.ConfigureAwait(false);
					if (success)
					{
						CoinRefrigerator.Freeze(finishedCoinJoin.CoinCandidates);
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
		}
	}

	private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
		WalletCoinJoinStates.Values
			.Where(x => x.Wallet.State == WalletState.Started) // Only running wallets
			.Where(x => x.CanStartAutoCoinJoin || x.Wallet.AllowManualCoinJoin)
			.Where(x => !x.Wallet.KeyManager.IsWatchOnly)      // that are not watch-only wallets
			.Where(x => x.Wallet.Kitchen.HasIngredients)
			.ToImmutableDictionary(x => x.Wallet.WalletName, x => x.Wallet);

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

	public override void Dispose()
	{
		WalletManager.WalletAdded -= WalletManager_WalletAdded;
		base.Dispose();
	}
}
