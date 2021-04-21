using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient
	{
		public AliceClient(Guid aliceId, Guid roundId, ArenaClient arenaClient, Coin coin, FeeRate feeRate)
		{
			AliceId = aliceId;
			RoundId = roundId;
			ArenaClient = arenaClient;
			Coin = coin;
			FeeRate = feeRate;
		}

		public Guid AliceId { get; }
		public Guid RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private Coin Coin { get; }
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
			var inputVsize = Constants.P2wpkhInputVirtualSize;
			var inputRemainingVsizes = new[] { (long)ProtocolConstants.MaxVsizePerAlice - inputVsize };

			var amountCredentials = ArenaClient.AmountCredentialClient.Credentials;

			var totalFeeToPay = FeeRate.GetFee(Coin.ScriptPubKey.EstimateInputVsize());
			var totalAmount = Coin.Amount;
			var effectiveAmount = totalAmount - totalFeeToPay;

			if (effectiveAmount <= Money.Zero)
			{
				throw new InvalidOperationException($"Round({ RoundId }), Alice({ AliceId}): Not enough funds to pay for the fees.");
			}

			var amountsToRequest = new[] { effectiveAmount };

			return await ArenaClient
				.ConfirmConnectionAsync(
					RoundId,
					AliceId,
					inputRemainingVsizes,
					amountCredentials.ZeroValue.Take(ProtocolConstants.CredentialNumber),
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
			await ArenaClient.SignTransactionAsync(RoundId, Coin, bitcoinSecret, unsignedCoinJoin).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}

		public static async Task<AliceClient> CreateNewAsync(
			ArenaClient arenaClient,
			Coin coin,
			BitcoinSecret bitcoinSecret,
			Guid roundId,
			uint256 roundHash,
			FeeRate feeRate)
		{
			Guid aliceId = await arenaClient.RegisterInputAsync(coin.Amount, coin.Outpoint, bitcoinSecret.PrivateKey, roundId, roundHash).ConfigureAwait(false);

			AliceClient client = new(aliceId, roundId, arenaClient, coin, feeRate);

			Logger.LogInfo($"Round ({roundId}), Alice ({aliceId}): Registered an input.");

			return client;
		}
	}
}
