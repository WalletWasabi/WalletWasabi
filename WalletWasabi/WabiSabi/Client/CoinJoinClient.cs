using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinClient : BackgroundService, IDisposable
	{
		private bool _disposedValue;
		private CredentialPool AmountCredentialPool { get; } = new();
		private CredentialPool VsizeCredentialPool { get; } = new();
		private Round Round { get; }
		public IArenaRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private SecureRandom SecureRandom { get; }
		private CancellationTokenSource DisposeCts { get; } = new();
		private Coin[] Coins { get; set; }
		private Random Random { get; } = new();

		public CoinJoinClient(
			Round round,
			IArenaRequestHandler arenaRequestHandler,
			IEnumerable<Coin> coins,
			Kitchen kitchen,
			KeyManager keymanager)
		{
			Round = round;
			ArenaRequestHandler = arenaRequestHandler;
			Kitchen = kitchen;
			Keymanager = keymanager;
			SecureRandom = new SecureRandom();
			Coins = coins.ToArray();
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				// Register coins.
				AliceClient[] aliceClients = await RegisterCoinsAsync().ConfigureAwait(false);

				// Confirm coins.
				await ConfirmConnectionsAsync(aliceClients, stoppingToken).ConfigureAwait(false);

				// Planning
				var outputs = await DecomposeAmountsAsync(stoppingToken).ConfigureAwait(false);

				// Output registration.
				await RegisterOutputsAsync(outputs).ConfigureAwait(false);

				Transaction? unsignedCoinJoinTransaction = null; // TODO: Get it from somewhere.

				// Send signature.
				await SignTransactionAsync(aliceClients, unsignedCoinJoinTransaction).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// The game is over for this round, no fallback mechanism. In the next round we will create another CoinJoinClient and try again.
			}
		}

		public async Task StartMixingCoinsAsync()
		{
			await StartAsync(DisposeCts.Token).ConfigureAwait(false);
		}

		private async Task<AliceClient[]> RegisterCoinsAsync()
		{
			var aliceArenaClient = new ArenaClient(
				Round.AmountCredentialIssuerParameters,
				Round.VsizeCredentialIssuerParameters,
				AmountCredentialPool,
				VsizeCredentialPool,
				ArenaRequestHandler,
				SecureRandom);

			List<AliceClient> aliceClients = new();

			foreach (var coin in Coins)
			{
				var secret = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey.WitHash.ScriptPubKey).First().PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				aliceClients.Add(await AliceClient.CreateNewAsync(aliceArenaClient, coin, secret, Round.Id, Round.FeeRate).ConfigureAwait(false));
				await Task.Delay(Random.Next(0, 1000)).ConfigureAwait(false);
			}

			return aliceClients.ToArray();
		}

		private async Task ConfirmConnectionsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken stoppingToken)
		{
			foreach (var alice in aliceClients)
			{
				await alice.ConfirmConnectionAsync(TimeSpan.FromMilliseconds(Random.Next(1000, 5000)), stoppingToken).ConfigureAwait(false);
				await Task.Delay(Random.Next(0, 1000), stoppingToken).ConfigureAwait(false);
			}
		}

		private async Task RegisterOutputsAsync((Money Amount, HdPubKey Pubkey)[] outputs)
		{
			ArenaClient bobArenaClient = new(
				Round.AmountCredentialIssuerParameters,
				Round.VsizeCredentialIssuerParameters,
				AmountCredentialPool,
				VsizeCredentialPool,
				ArenaRequestHandler,
				SecureRandom);

			foreach (var output in outputs)
			{
				BobClient bobClient = new(Round.Id, bobArenaClient);
				await bobClient.RegisterOutputAsync(output.Amount, output.Pubkey.PubKey.WitHash.ScriptPubKey).ConfigureAwait(false);
				// Random delay?
			}
		}

		private async Task<(Money Amount, HdPubKey Pubkey)[]> DecomposeAmountsAsync(CancellationToken stoppingToken)
		{
			// Simulate planner: https://github.com/zkSNACKs/WalletWasabi/pull/5694#discussion_r622468552.
			await Task.Delay(0, stoppingToken).ConfigureAwait(false);
			var amounts = Enumerable.Repeat(Money.Zero, 4).ToArray();

			return amounts.Select(amount => (amount, Keymanager.GenerateNewKey("", KeyState.Locked, true, true))).ToArray(); // Keymanager threadsafe => no!?
		}

		private async Task SignTransactionAsync(AliceClient[] aliceClients, Transaction unsignedCoinJoinTransaction)
		{
			foreach (var aliceClient in aliceClients)
			{
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction).ConfigureAwait(false);
			}
		}

		public async Task StopAsync()
		{
			await StopAsync().ConfigureAwait(false);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					DisposeCts.Cancel();
					SecureRandom.Dispose();
				}
				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
