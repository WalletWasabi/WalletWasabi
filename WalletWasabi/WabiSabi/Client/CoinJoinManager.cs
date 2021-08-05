using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
			await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken).ConfigureAwait(false);
			while (!stoppingToken.IsCancellationRequested)
			{
				var currentRoundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, stoppingToken).ConfigureAwait(false);

				var mixableWalletsAndCoins = GetMixableWallets()
					.Select(wallet => (Wallet: wallet, Coins: SelectCoinsForWallet(wallet, currentRoundState)));

				var coinjoinClients = mixableWalletsAndCoins
					.Select(x => new CoinJoinClient(ArenaRequestHandler, x.Coins, x.Wallet.Kitchen, x.Wallet.KeyManager, RoundStatusUpdater));

				await Task.WhenAll(
					coinjoinClients.Select(async x => await x.StartCoinJoinAsync(stoppingToken))).ConfigureAwait(false);
			}
		}

		private IEnumerable<Coin> SelectCoinsForWallet(Wallet wallet, RoundState roundState) =>
			wallet.Coins.Available().Confirmed()
				.Where(x => x.HdPubKey.AnonymitySet < ServiceConfiguration.GetMixUntilAnonymitySetValue())
				.Where(x => roundState.CoinjoinState.Parameters.AllowedInputAmounts.Contains(x.Amount) )
				.Where(x => roundState.CoinjoinState.Parameters.AllowedInputTypes.Any(t => x.ScriptPubKey.IsScriptType(t)))
				.OrderByDescending(x => x.Amount)
				.Take(MaxInputsRegistrableByWallet)
				.Select(x => x.Coin);

		private IEnumerable<Wallet> GetMixableWallets() =>
			WalletManager.GetWallets()
				.Where(x => x.State == WalletState.Started) // Only running wallets
				.Where(x => x.KeyManager.AutoCoinJoin)		// configured to be mixed automatically
				.Where(x => !x.KeyManager.IsWatchOnly)		// that are not watch-only wallets
				.Where(x => x.Kitchen.HasIngredients);
	}
}