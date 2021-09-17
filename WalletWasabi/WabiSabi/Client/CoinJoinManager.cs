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
		public CoinJoinManager(WalletManager walletManager, RoundStateUpdater roundStatusUpdater, HttpClientFactory backendHttpClientFactory, ServiceConfiguration serviceConfiguration)
		{
			WalletManager = walletManager;
			ArenaRequestHandler = new WabiSabiHttpApiClient(backendHttpClientFactory.NewBackendHttpClient(Mode.SingleCircuitPerLifetime));
			RoundStatusUpdater = roundStatusUpdater;
			ServiceConfiguration = serviceConfiguration;
		}

		public WalletManager WalletManager { get; }
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public RoundStateUpdater RoundStatusUpdater { get; }
		public ServiceConfiguration ServiceConfiguration { get; }
		private ImmutableDictionary<string, WalletTrackingData> TrackedWallets { get; set; } = ImmutableDictionary<string, WalletTrackingData>.Empty;
		private List<(SmartCoin Coin, DateTimeOffset ExpirationTime)> CoinsInQuarantine { get; } = new ();

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
					var coinjoinClient = new CoinJoinClient(ArenaRequestHandler, openedWallet.Kitchen, openedWallet.KeyManager, RoundStatusUpdater);
					var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
					IEnumerable<SmartCoin> coinCandidates = SelectCandidateCoins(openedWallet);
					var coinjoinTask = coinjoinClient.StartCoinJoinAsync(coinCandidates, cts.Token);

					trackedWallets.Add(openedWallet.WalletName, new WalletTrackingData(openedWallet, coinjoinClient, coinjoinTask, coinCandidates.ToArray(), cts));
				}

				foreach (var closedWallet in closedWallets.Select(x => x.Value))
				{
					closedWallet.CancellationTokenSource.Cancel();
					closedWallet.CancellationTokenSource.Dispose();
				}

				var finishedCoinJoins = trackedWallets
					.Where(x => x.Value.CoinJoinTask.IsCompleted)
					.Select(x => x.Value)
					.ToImmutableArray();

				foreach (var finishedCoinJoin in finishedCoinJoins)
				{
					var removed = trackedWallets.Remove(finishedCoinJoin.Wallet.WalletName);
					if (!removed)
					{
						Logger.LogWarning($"Wallet: `{finishedCoinJoin.Wallet.WalletName}` was not removed from tracked wallet list. Will retry in a few seconds.");
					}
					finishedCoinJoin.CancellationTokenSource.Dispose();
				}

				foreach (var finishedCoinJoin in finishedCoinJoins)
				{
					var logPrefix = $"Wallet: `{finishedCoinJoin.Wallet.WalletName}` - Coinjoin client";
					try
					{
						var success = await finishedCoinJoin.CoinJoinTask.ConfigureAwait(false);
						if (success)
						{
							Logger.LogInfo($"{logPrefix} finished successfully!");
						}
						else
						{
							Logger.LogInfo($"{logPrefix} finished with error. Transaction not broadcasted.");
						}
						QuarantineParticipantCoins(finishedCoinJoin.Coins);
					}
					catch (InvalidOperationException ioe)
					{
						Logger.LogError(ioe);
						QuarantineParticipantCoins(finishedCoinJoin.Coins);
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
				CleanQuarantineCoins();
			}
		}

		private IEnumerable<SmartCoin> SelectCandidateCoins(Wallet openedWallet)
		{
			var now = DateTimeOffset.UtcNow;
			var coinsStillInQuarantine = CoinsInQuarantine
				.Where(x => x.ExpirationTime > now)
				.Select(x => x.Coin)
				.ToHashSet();

			return openedWallet.Coins
				.Available()
				.Confirmed()
				.Where(x => x.HdPubKey.AnonymitySet < ServiceConfiguration.GetMixUntilAnonymitySetValue())
				.Where(x => !coinsStillInQuarantine.Contains(x));
		}

		private void QuarantineParticipantCoins(SmartCoin[] coins)
		{
			var expirationDate = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90);

			foreach (var coin in coins)
			{
				CoinsInQuarantine.Add((coin, expirationDate));
			}
		}

		private void CleanQuarantineCoins()
		{
			CoinsInQuarantine.RemoveAll(x => x.ExpirationTime < DateTimeOffset.UtcNow);
		}

		private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
			WalletManager.GetWallets()
				.Where(x => x.State == WalletState.Started) // Only running wallets
				.Where(x => x.KeyManager.AutoCoinJoin || x.AllowManualCoinJoin)     // configured to be mixed automatically or manually
				.Where(x => !x.KeyManager.IsWatchOnly)      // that are not watch-only wallets
				.Where(x => x.Kitchen.HasIngredients)
				.ToImmutableDictionary(x => x.WalletName, x => x);

		private record WalletTrackingData(Wallet Wallet, CoinJoinClient CoinJoinClient, Task<bool> CoinJoinTask, SmartCoin[] Coins, CancellationTokenSource CancellationTokenSource);
	}
}
