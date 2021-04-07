using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient : BackgroundService, IAsyncDisposable
	{
		public Guid AliceId { get; }
		public Guid RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private IEnumerable<Coin> Coins { get; }

		public AliceClient(Guid aliceId, Guid roundId, ArenaClient arenaClient, IEnumerable<Coin> coins)
		{
			AliceId = aliceId;
			RoundId = roundId;
			ArenaClient = arenaClient;
			Coins = coins;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			do
			{
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
			}
			while (!await ConfirmConnectionAsync().ConfigureAwait(false));
		}

		private async Task<bool> ConfirmConnectionAsync()
		{
			var inputWeight = 4 * Constants.P2wpkhInputVirtualSize;
			var inputRemainingWeights = new[] { (long)ArenaClient.ProtocolMaxWeightPerAlice - inputWeight };

			var amountCredentials = ArenaClient.AmountCredentialClient.Credentials;

			await ArenaClient.ConfirmConnectionAsync(
				RoundId,
				AliceId,
				inputRemainingWeights,
				amountCredentials.ZeroValue.Take(ArenaClient.ProtocolCredentialNumber),
				Coins.Select(c => c.Amount)
				).ConfigureAwait(false);

			return ArenaClient.AmountCredentialClient.Credentials.Valuable.Any();
		}

		public async Task RemoveInputAsync()
		{
			await ArenaClient.RemoveInputAsync(RoundId, AliceId).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin)
		{
			await ArenaClient.SignTransactionAsync(RoundId, Coins, bitcoinSecret, unsignedCoinJoin).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted {Coins.Count()} signatures.");
		}

		public static async Task<AliceClient> CreateNewAsync(
			ArenaClient arenaClient,
			IEnumerable<Coin> coinsToRegister,
			BitcoinSecret bitcoinSecret,
			Guid roundId,
			uint256 roundHash)
		{
			IEnumerable<Money> amounts = coinsToRegister.Select(c => c.Amount);
			IEnumerable<OutPoint> outPoints = coinsToRegister.Select(c => c.Outpoint);
			IEnumerable<Key> keys = Enumerable.Repeat(bitcoinSecret.PrivateKey, coinsToRegister.Count());

			Guid aliceId = await arenaClient.RegisterInputAsync(amounts, outPoints, keys, roundId, roundHash).ConfigureAwait(false);

			AliceClient client = new(aliceId, roundId, arenaClient, coinsToRegister);

			Logger.LogInfo($"Round ({roundId}), Alice ({aliceId}): Registered {amounts.Count()} inputs.");

			await client.StartAsync(CancellationToken.None).ConfigureAwait(false);

			return client;
		}

		public async ValueTask DisposeAsync()
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}
}
