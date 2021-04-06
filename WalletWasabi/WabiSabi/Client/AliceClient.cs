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
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient : BackgroundService, IAsyncDisposable
	{
		private Guid AliceId { get; }
		private Guid RoundId { get; }
		private ArenaClient ArenaClient { get; }
		public IEnumerable<ICoin> CoinsToRegister { get; }

		public AliceClient(Guid aliceId, Guid roundId, ArenaClient arenaClient, IEnumerable<ICoin> coinsToRegister)
		{
			AliceId = aliceId;
			RoundId = roundId;
			ArenaClient = arenaClient;
			CoinsToRegister = coinsToRegister;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Input registration phase
			do
			{
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
				await ConfirmationAsync().ConfigureAwait(false);
			}
			while (!stoppingToken.IsCancellationRequested);
		}

		private async Task ConfirmationAsync()
		{
			var inputWeight = 4 * Constants.P2wpkhInputVirtualSize;
			var inputRemainingWeights = new[] { (long)ArenaClient.ProtocolMaxWeightPerAlice - inputWeight };

			var amountCredentials = ArenaClient.AmountCredentialClient.Credentials;

			await ArenaClient.ConfirmConnectionAsync(
				RoundId,
				AliceId,
				inputRemainingWeights,
				amountCredentials.ZeroValue.Take(ArenaClient.ProtocolCredentialNumber),
				CoinsToRegister.Select(c => (Money)c.Amount)
				).ConfigureAwait(false);
		}

		public async Task UnConfirmationAsync()
		{
			throw new NotImplementedException();
			// TODO: Add .RequestHandler.RemoveInputAsync() to ArenaClient.
			// Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Unconfirmed connection.");
		}

		public async Task PostSignaturesAsync(BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin)
		{
			await ArenaClient.SignTransactionAsync(RoundId, CoinsToRegister, bitcoinSecret, unsignedCoinJoin).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted {CoinsToRegister.Count()} signatures.");
		}

		public static async Task<AliceClient> CreateNewAsync(
			ArenaClient arenaClient,
			IEnumerable<ICoin> coinsToRegister,
			BitcoinSecret bitcoinSecret,
			Guid roundId,
			uint256 roundHash)
		{
			IEnumerable<Money> amounts = coinsToRegister.Select(c => (Money)c.Amount);
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
