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
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinManager : BackgroundService
	{
		private const int MaxInputsRegistrableByWallet = 7; // how many

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
		//public HttpClientFactory BackendHttpClientFactory { get; }

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var trackedWallets = new Dictionary<string, WalletTrackingData>();
			Task<RoundState> roundUpdateTask = RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, stoppingToken);
			var currentRoundState = await roundUpdateTask.ConfigureAwait(false);

			while (!stoppingToken.IsCancellationRequested)
			{
				var delayTask = Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
				var completedTask = await Task.WhenAny(
					roundUpdateTask,
					delayTask
				).ConfigureAwait(false);

				if (completedTask == roundUpdateTask)
				{
					currentRoundState = await roundUpdateTask.ConfigureAwait(false);
					roundUpdateTask = RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, stoppingToken);
				}
				Logger.LogInfo($"Current round: {currentRoundState.Id} - ({currentRoundState.Phase} ({currentRoundState.BlameOf} )");
				var mixableWallets = GetMixableWallets();
				var openedWallets = mixableWallets.Where(x => !trackedWallets.ContainsKey(x.Key));
				var closedWallets = trackedWallets.Where(x => !mixableWallets.ContainsKey(x.Key));

				foreach (var openedWallet in openedWallets.Select(x => x.Value))
				{
					var coins = SelectCoinsForWallet(openedWallet, currentRoundState);
					var coinjoinClient = new CoinJoinClient(ArenaRequestHandler, coins, openedWallet.Kitchen, openedWallet.KeyManager, RoundStatusUpdater);
					var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
					var coinjoinTask = coinjoinClient.StartCoinJoinAsync(cts.Token);

					trackedWallets.Add(openedWallet.WalletName, new WalletTrackingData(openedWallet, coinjoinTask, cts));
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
					trackedWallets.Remove(finishedCoinJoin.Wallet.WalletName);
					finishedCoinJoin.CancellationTokenSource.Dispose();

					var finishedCoinJoinTask = finishedCoinJoin.CoinJoinTask;
					if (finishedCoinJoinTask.IsCompletedSuccessfully)
					{
						Logger.LogInfo("Coinjoin client finished successfully!");
					}
					else if (finishedCoinJoinTask.IsFaulted)
					{
						Logger.LogError(finishedCoinJoinTask.Exception!);
					}
					else if (finishedCoinJoinTask.IsCanceled)
					{
						Logger.LogInfo("Coinjoin client was cancelled.");
					}
				}
			}
		}

		private ImmutableList<SmartCoin> SelectCoinsForWallet(Wallet wallet, RoundState roundState) =>
			wallet.Coins.Available().Confirmed()
				.Where(x => x.HdPubKey.AnonymitySet < ServiceConfiguration.GetMixUntilAnonymitySetValue())
				.Where(x => roundState.CoinjoinState.Parameters.AllowedInputAmounts.Contains(x.Amount) )
				.Where(x => roundState.CoinjoinState.Parameters.AllowedInputTypes.Any(t => x.ScriptPubKey.IsScriptType(t)))
				.OrderBy(x => x.HdPubKey.AnonymitySet)
				.ThenByDescending(x => x.Amount)
				.Take(MaxInputsRegistrableByWallet)
				.ToImmutableList();

		private ImmutableDictionary<string, Wallet> GetMixableWallets() =>
			WalletManager.GetWallets()
				.Where(x => x.State == WalletState.Started) // Only running wallets
				.Where(x => x.KeyManager.AutoCoinJoin)		// configured to be mixed automatically
				.Where(x => !x.KeyManager.IsWatchOnly)		// that are not watch-only wallets
				.Where(x => x.Kitchen.HasIngredients)
				.ToImmutableDictionary(x => x.WalletName, x => x);

		record WalletTrackingData(Wallet Wallet, Task<bool> CoinJoinTask, CancellationTokenSource CancellationTokenSource);
	}
}