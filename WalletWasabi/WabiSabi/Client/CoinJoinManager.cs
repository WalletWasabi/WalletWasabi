using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinManager : BackgroundService
	{
		public CoinJoinManager(WalletManager walletManager, RoundStateUpdater roundStatusUpdater, IBackendHttpClientFactory backendHttpClientFactory, ServiceConfiguration serviceConfiguration)
		{
			WalletManager = walletManager;
			HttpClientFactory = backendHttpClientFactory;
			RoundStatusUpdater = roundStatusUpdater;
			ServiceConfiguration = serviceConfiguration;
		}

		public event EventHandler<WalletStatusChangedEventArgs>? WalletStatusChanged;

		public WalletManager WalletManager { get; }
		public IBackendHttpClientFactory HttpClientFactory { get; }
		public RoundStateUpdater RoundStatusUpdater { get; }
		public ServiceConfiguration ServiceConfiguration { get; }
		private ImmutableDictionary<string, WalletTrackingData> TrackedWallets { get; set; } = ImmutableDictionary<string, WalletTrackingData>.Empty;
		private CoinRefrigerator CoinRefrigerator { get; } = new();

		public CoinJoinClientState HighestCoinJoinClientState
		{
			get
			{
				var inProgress = TrackedWallets.Values.Where(wtd => !wtd.CoinJoinTask.IsCompleted).ToImmutableArray();

				if (inProgress.IsEmpty)
				{
					return CoinJoinClientState.Idle;
				}

				return inProgress.Any(wtd => wtd.CoinJoinClient.InCriticalCoinJoinState)
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
			var trackedWallets = new Dictionary<string, WalletTrackingData>();

			while (!stoppingToken.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
				var mixableWallets = GetMixableWallets();
				var openedWallets = mixableWallets.Where(x => !trackedWallets.ContainsKey(x.Key));
				var closedWallets = trackedWallets.Where(x => !mixableWallets.ContainsKey(x.Key));

				foreach (var openedWallet in openedWallets.Select(x => x.Value))
				{
					var coinjoinClient = new CoinJoinClient(HttpClientFactory, openedWallet.Kitchen, openedWallet.KeyManager, RoundStatusUpdater);
					var coinCandidates = SelectCandidateCoins(openedWallet).ToArray();
					if (coinCandidates.Length == 0)
					{
						continue;
					}

					var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
					var coinjoinTask = coinjoinClient.StartCoinJoinAsync(coinCandidates, cts.Token);

					trackedWallets.Add(openedWallet.WalletName, new WalletTrackingData(openedWallet, coinjoinClient, coinjoinTask, coinCandidates, cts));
					WalletStatusChanged?.Invoke(this, new WalletStatusChangedEventArgs(openedWallet, IsCoinJoining: true));
				}

				foreach (var closedWallet in closedWallets.Select(x => x.Value))
				{
					closedWallet.CancellationTokenSource.Cancel();
				}

				var finishedCoinJoins = trackedWallets
					.Where(x => x.Value.CoinJoinTask.IsCompleted)
					.Select(x => x.Value)
					.ToImmutableArray();

				foreach (var finishedCoinJoin in finishedCoinJoins)
				{
					var walletToRemove = finishedCoinJoin.Wallet;
					if (!trackedWallets.Remove(walletToRemove.WalletName))
					{
						Logger.LogWarning($"Wallet: `{walletToRemove.WalletName}` was not removed from tracked wallet list. Will retry in a few seconds.");
					}
					else
					{
						WalletStatusChanged?.Invoke(this, new WalletStatusChangedEventArgs(walletToRemove, IsCoinJoining: false));
						finishedCoinJoin.CancellationTokenSource.Dispose();
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
							Logger.LogInfo($"{logPrefix} finished successfully!");
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
				}

				TrackedWallets = trackedWallets.ToImmutableDictionary();
			}
		}

		private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
			WalletManager.GetWallets()
				.Where(x => x.State == WalletState.Started) // Only running wallets
				.Where(x => x.KeyManager.AutoCoinJoin || x.AllowManualCoinJoin)     // configured to be mixed automatically or manually
				.Where(x => !x.KeyManager.IsWatchOnly)      // that are not watch-only wallets
				.Where(x => x.Kitchen.HasIngredients)
				.ToImmutableDictionary(x => x.WalletName, x => x);

		private IEnumerable<SmartCoin> SelectCandidateCoins(Wallet openedWallet)
		{
			return openedWallet.Coins
				.Available()
				.Confirmed()
				.Where(x => x.HdPubKey.AnonymitySet < ServiceConfiguration.GetMixUntilAnonymitySetValue())
				.Where(x => !CoinRefrigerator.IsFrozen(x));
		}

		private record WalletTrackingData(Wallet Wallet, CoinJoinClient CoinJoinClient, Task<bool> CoinJoinTask, IEnumerable<SmartCoin> CoinCandidates, CancellationTokenSource CancellationTokenSource);

		public async Task PullOutCoinsFromCoinJoinAsync(IEnumerable<SmartCoin> coinsToPullOutCoins, CancellationToken cancel = default)
		{
			var coins = coinsToPullOutCoins.ToHashSet();

			// Freeze the coins.
			CoinRefrigerator.Freeze(coins);

			var walletsToAbort = TrackedWallets.Values.Where(wtd => wtd.CoinCandidates.Any(coins.Contains));
			foreach (var wallet in walletsToAbort)
			{
				wallet.CancellationTokenSource.Cancel();
			}

			while (TrackedWallets.Values.Any(wtd => wtd.CoinCandidates.Any(coins.Contains)) || coins.Any(c => c.CoinJoinInProgress))
			{
				await Task.Delay(1000, cancel).ConfigureAwait(false);
			}
		}
	}
}
