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
	public class AliceClient
	{
		public AliceClient(Guid aliceId, Guid roundId, ArenaClient arenaClient, IEnumerable<Coin> coins, FeeRate feeRate)
		{
			AliceId = aliceId;
			RoundId = roundId;
			ArenaClient = arenaClient;
			Coins = coins;
			FeeRate = feeRate;
		}

		public Guid AliceId { get; }
		public Guid RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private IEnumerable<Coin> Coins { get; }
		private FeeRate FeeRate { get; }

		public async Task ConfirmConnectionAsync(TimeSpan confirmInterval, CancellationToken token)
		{
			while (!await ConfirmConnectionAsync().ConfigureAwait(false))
			{
				await Task.Delay(confirmInterval, token).ConfigureAwait(false);
			}
		}

		private async Task<bool> ConfirmConnectionAsync()
		{
			var inputWeight = 4 * Constants.P2wpkhInputVirtualSize;
			var inputRemainingWeights = new[] { (long)ArenaClient.ProtocolMaxWeightPerAlice - inputWeight };

			var amountCredentials = ArenaClient.AmountCredentialClient.Credentials;

			var totalFeeToPay = FeeRate.GetFee(Coins.Sum(c => c.ScriptPubKey.EstimateInputVsize()));
			var totalAmount = Coins.Sum(coin => coin.Amount);

			if (totalFeeToPay > totalAmount)
			{
				throw new InvalidOperationException($"Round({ RoundId }), Alice({ AliceId}): Not enough funds to pay for the fees.");
			}

			var amountsToRequest = new[] { totalAmount - totalFeeToPay };

			return await ArenaClient
				.ConfirmConnectionAsync(
					RoundId,
					AliceId,
					inputRemainingWeights,
					amountCredentials.ZeroValue.Take(ArenaClient.ProtocolCredentialNumber),
					amountsToRequest)
				.ConfigureAwait(false);
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
			uint256 roundHash,
			FeeRate feeRate)
		{
			IEnumerable<Money> amounts = coinsToRegister.Select(c => c.Amount);
			IEnumerable<OutPoint> outPoints = coinsToRegister.Select(c => c.Outpoint);
			IEnumerable<Key> keys = Enumerable.Repeat(bitcoinSecret.PrivateKey, coinsToRegister.Count());

			Guid aliceId = await arenaClient.RegisterInputAsync(amounts, outPoints, keys, roundId, roundHash).ConfigureAwait(false);

			AliceClient client = new(aliceId, roundId, arenaClient, coinsToRegister, feeRate);

			Logger.LogInfo($"Round ({roundId}), Alice ({aliceId}): Registered {amounts.Count()} inputs.");

			return client;
		}
	}
}
