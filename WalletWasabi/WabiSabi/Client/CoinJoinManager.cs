using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Concurrent;
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
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
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
		private ConcurrentDictionary<string, WalletTrackingData> TrackedWallets { get; } = new();

		public CoinJoinClientState HighestCoinJoinClientState
		{
			get
			{
				var coinjoinClients = TrackedWallets.Values;
				if (coinjoinClients.Any(wt => wt.CoinJoinClient.State is CoinJoinClientState.InCriticalPhase))
				{
					return CoinJoinClientState.InCriticalPhase;
				}

				return coinjoinClients.Any(wt => wt.CoinJoinClient.State is CoinJoinClientState.InProgress)
					? CoinJoinClientState.InProgress
					: CoinJoinClientState.Idle;
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			if (WalletManager.Network == Network.Main)
			{
				Logger.LogInfo("WabiSabi coinjoin client-side functionality is disabled temporarily on mainnet.");
				return;
			}

			while (!stoppingToken.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
				var mixableWallets = GetMixableWallets();
				var openedWallets = mixableWallets.Where(x => !TrackedWallets.ContainsKey(x.Key));
				var closedWallets = TrackedWallets.Where(x => !mixableWallets.ContainsKey(x.Key));

				foreach (var openedWallet in openedWallets.Select(x => x.Value))
				{
					var coinjoinClient = new CoinJoinClient(ArenaRequestHandler, openedWallet.Kitchen, openedWallet.KeyManager, RoundStatusUpdater);
					var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
					var coinCandidates = openedWallet.Coins.Available().Confirmed().Where(x => x.HdPubKey.AnonymitySet < ServiceConfiguration.GetMixUntilAnonymitySetValue());
					var coinjoinTask = coinjoinClient.StartCoinJoinAsync(coinCandidates, cts.Token);

					TrackedWallets.TryAdd(openedWallet.WalletName, new WalletTrackingData(openedWallet, coinjoinClient, coinjoinTask, cts));
				}

				foreach (var closedWallet in closedWallets.Select(x => x.Value))
				{
					closedWallet.CancellationTokenSource.Cancel();
					closedWallet.CancellationTokenSource.Dispose();
				}

				var finishedCoinJoins = TrackedWallets
					.Where(x => x.Value.CoinJoinTask.IsCompleted)
					.Select(x => x.Value)
					.ToImmutableArray();

				foreach (var finishedCoinJoin in finishedCoinJoins)
				{
					TrackedWallets.TryRemove(finishedCoinJoin.Wallet.WalletName, out _);
					finishedCoinJoin.CancellationTokenSource.Dispose();

					var finishedCoinJoinTask = finishedCoinJoin.CoinJoinTask;
					if (finishedCoinJoinTask.IsCompletedSuccessfully)
					{
						Logger.LogInfo("Coinjoin client finished successfully!");
					}
					else if (finishedCoinJoinTask.IsFaulted)
					{
						if (finishedCoinJoinTask.Exception?.InnerException is InvalidOperationException ioe)
						{
							await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
							continue;
						}
						Logger.LogError(finishedCoinJoinTask.Exception!);
					}
					else if (finishedCoinJoinTask.IsCanceled)
					{
						Logger.LogInfo("Coinjoin client was cancelled.");
					}
				}
			}
		}

		private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
			WalletManager.GetWallets()
				.Where(x => x.State == WalletState.Started) // Only running wallets
				.Where(x => x.KeyManager.AutoCoinJoin || x.AllowManualCoinJoin)     // configured to be mixed automatically or manually
				.Where(x => !x.KeyManager.IsWatchOnly)      // that are not watch-only wallets
				.Where(x => x.Kitchen.HasIngredients)
				.ToImmutableDictionary(x => x.WalletName, x => x);

		private record WalletTrackingData(Wallet Wallet, CoinJoinClient CoinJoinClient, Task<bool> CoinJoinTask, CancellationTokenSource CancellationTokenSource);
	}
}
