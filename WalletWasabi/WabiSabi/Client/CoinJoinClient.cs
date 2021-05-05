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
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinClient : BackgroundService, IDisposable
	{
		private bool _disposedValue;
		private CredentialPool AmountCredentialPool { get; } = new();
		private CredentialPool VsizeCredentialPool { get; } = new();
		private ClientRound Round { get; }
		public IArenaRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private SecureRandom SecureRandom { get; }
		private CancellationTokenSource DisposeCts { get; } = new();
		private IEnumerable<Coin> Coins { get; set; }
		private Random Random { get; } = new();

		public CoinJoinClient(
			ClientRound round,
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
			Coins = coins;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				var aliceClients = CreateAliceClients();

				// Register coins.
				await RegisterCoinsAsync(aliceClients, stoppingToken).ConfigureAwait(false);

				// Confirm coins.
				await ConfirmConnectionsAsync(aliceClients, stoppingToken).ConfigureAwait(false);

				// Planning
				ConstructionState constructionState = Round.CoinjoinState.AssertConstruction();
				var outputs = DecomposeAmounts(constructionState, stoppingToken);

				// Output registration.
				await ReissueAndRegisterOutputsAsync(outputs, stoppingToken).ConfigureAwait(false);

				SigningState signingState = Round.CoinjoinState.AssertSigning();
				// Send signature.
				await SignTransactionAsync(aliceClients, signingState.CreateUnsignedTransaction(), stoppingToken).ConfigureAwait(false);
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

		private IEnumerable<AliceClient> CreateAliceClients()
		{
			List<AliceClient> aliceClients = new();
			foreach (var coin in Coins)
			{
				var aliceArenaClient = new ArenaClient(
					Round.AmountCredentialIssuerParameters,
					Round.VsizeCredentialIssuerParameters,
					AmountCredentialPool,
					VsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom);

				var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey.WitHash.ScriptPubKey).Single();
				var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				aliceClients.Add(new AliceClient(Round.Id, aliceArenaClient, coin, Round.FeeRate, secret));
			}
			return aliceClients;
		}

		private async Task RegisterCoinsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken stoppingToken)
		{
			foreach (var aliceClient in aliceClients)
			{
				await aliceClient.RegisterInputAsync(stoppingToken).ConfigureAwait(false);
			}
		}

		private async Task ConfirmConnectionsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken stoppingToken)
		{
			foreach (var alice in aliceClients)
			{
				await alice.ConfirmConnectionAsync(TimeSpan.FromMilliseconds(Random.Next(1000, 5000)), stoppingToken).ConfigureAwait(false);
				await Task.Delay(Random.Next(0, 1000), stoppingToken).ConfigureAwait(false);
			}
		}

		private async Task ReissueAndRegisterOutputsAsync(IEnumerable<(Money Amount, HdPubKey Pubkey)> outputs, CancellationToken stoppingToken)
		{
			ArenaClient bobArenaClient = new(
				Round.AmountCredentialIssuerParameters,
				Round.VsizeCredentialIssuerParameters,
				AmountCredentialPool,
				VsizeCredentialPool,
				ArenaRequestHandler,
				SecureRandom);

			BobClient bobClient = new(Round.Id, bobArenaClient);

			Money remaining = outputs.Sum(o => o.Amount);

			var remainingAmountCredentials = AmountCredentialPool.Valuable.Single();
			var remainingVsizeCredentials = VsizeCredentialPool.Valuable.Single();

			foreach (var output in outputs)
			{
				var justNeedtheSize = output.Pubkey.PubKey.WitHash.ScriptPubKey;
				remaining -= output.Amount;

				var result = await bobArenaClient.ReissueCredentialAsync(
					Round.Id,
					output.Amount,
					output.Pubkey.PubKey.WitHash.ScriptPubKey,
					remaining,
					justNeedtheSize,
					new[] { remainingAmountCredentials },
					new[] { remainingVsizeCredentials },
					stoppingToken).ConfigureAwait(false);

				remainingAmountCredentials = result.RealAmountCredentials.Last();
				remainingVsizeCredentials = result.RealVsizeCredentials.Last();

				await bobClient.RegisterOutputAsync(
					output.Amount,
					output.Pubkey.PubKey.WitHash.ScriptPubKey,
					new[] { result.RealAmountCredentials.First() },
					new[] { result.RealVsizeCredentials.First() },
					stoppingToken).ConfigureAwait(false);

				await Task.Delay(Random.Next(0, 1000), stoppingToken).ConfigureAwait(false);
			}
		}

		private IEnumerable<(Money Amount, HdPubKey Pubkey)> DecomposeAmounts(ConstructionState construction, CancellationToken stoppingToken)
		{
			const int Count = 4;

			// Simple decomposer.
			Money total = Coins.Sum(c => c.Amount) - Round.FeeRate.GetFee(Helpers.Constants.P2wpkhInputVirtualSize);
			Money amount = total / Count;

			List<Money> amounts = Enumerable.Repeat(Money.Satoshis(amount), Count - 1).ToList();
			amounts.Add(total - amounts.Sum());

			return amounts.Select(amount => (amount, Keymanager.GenerateNewKey("", KeyState.Locked, true, true))).ToArray(); // Keymanager threadsafe => no!?
		}

		private async Task SignTransactionAsync(IEnumerable<AliceClient> aliceClients, Transaction unsignedCoinJoinTransaction, CancellationToken stoppingToken)
		{
			foreach (var aliceClient in aliceClients)
			{
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, stoppingToken).ConfigureAwait(false);
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
