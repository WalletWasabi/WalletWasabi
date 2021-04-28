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
		private BitcoinSecret BitcoinSecret { get; }
		public KeyManager Keymanager { get; }
		private SecureRandom SecureRandom { get; }
		private CancellationTokenSource DisposeCts { get; } = new();
		private Coin[] Coins { get; set; }

		public CoinJoinClient(
			Round round,
			IArenaRequestHandler arenaRequestHandler,
			BitcoinSecret bitcoinSecret,
			IEnumerable<Coin> coins,
			KeyManager keymanager)
		{
			Round = round;
			ArenaRequestHandler = arenaRequestHandler;
			BitcoinSecret = bitcoinSecret;
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

				Money[]? othersInputAmounts = null; // TODO: Get it from somewhere.

				// Planning
				var outputs = await PlanAmountsAsync(othersInputAmounts, stoppingToken).ConfigureAwait(false);

				// Output registration.
				await RegisterOutputsAsync(outputs).ConfigureAwait(false);

				Transaction? unsignedCoinJoinTransaction = null; // TODO: Get it from somewhere.

				// Send signature.
				await SignTransactionAsync(aliceClients, unsignedCoinJoinTransaction).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
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
			try
			{
				foreach (var coin in Coins)
				{
					// Parallelize or Random delay?
					aliceClients.Add(await AliceClient.CreateNewAsync(aliceArenaClient, coin, BitcoinSecret, Round.Id, Round.Hash, Round.FeeRate).ConfigureAwait(false));
				}
			}
			catch (Exception)
			{
				foreach (var alice in aliceClients)
				{
					// Remove already registered Inputs.
					try
					{
						await alice.RemoveInputAsync().ConfigureAwait(false);
					}
					catch (Exception)
					{
						// Log?
					}
				}
				throw;
			}

			return aliceClients.ToArray();
		}

		private async Task ConfirmConnectionsAsync(AliceClient[] aliceClients, CancellationToken stoppingToken)
		{
			Task[] confirmTasks = aliceClients.Select(aliceClient => aliceClient.ConfirmConnectionAsync(TimeSpan.FromSeconds(5), stoppingToken)).ToArray();

			await Task.WhenAll(confirmTasks).ConfigureAwait(false);

			var exceptions = confirmTasks.Where(t => t.IsFaulted && t.Exception is { }).Select(t => t.Exception);
			if (exceptions.Any())
			{
				// Error! Try to de-register inputs?
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
				BobClient bobClient = new BobClient(Round.Id, bobArenaClient);
				await bobClient.RegisterOutputAsync(output.Amount, output.Pubkey.PubKey.WitHash.ScriptPubKey).ConfigureAwait(false);
				// Random delay?
			}
		}

		private async Task<(Money Amount, HdPubKey Pubkey)[]> PlanAmountsAsync(Money[]? inputsOfOthers, CancellationToken stoppingToken)
		{
			// Simulate planner.
			await Task.Delay(0, stoppingToken).ConfigureAwait(false);
			var amounts = Enumerable.Repeat(Money.Zero, 4).ToArray();

			return amounts.Select(amount => (amount, Keymanager.GenerateNewKey("", KeyState.Locked, true, true))).ToArray(); // Keymanager threadsafe => no!?
		}

		private async Task SignTransactionAsync(AliceClient[] aliceClients, Transaction unsignedCoinJoinTransaction)
		{
			foreach (var aliceClient in aliceClients)
			{
				await aliceClient.SignTransactionAsync(BitcoinSecret, unsignedCoinJoinTransaction).ConfigureAwait(false);
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
