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
		public AliceClient(uint256 aliceId, uint256 roundId, ArenaClient arenaClient, Coin coin, FeeRate feeRate)
		{
			AliceId = aliceId;
			RoundId = roundId;
			ArenaClient = arenaClient;
			Coin = coin;
			FeeRate = feeRate;
		}

		public uint256 AliceId { get; }
		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private Coin Coin { get; }
		private FeeRate FeeRate { get; }

		public async Task ConfirmConnectionAsync(TimeSpan confirmInterval, CancellationToken cancellationToken)
		{
			while (!await ConfirmConnectionAsync(cancellationToken).ConfigureAwait(false))
			{
				await Task.Delay(confirmInterval, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task<bool> ConfirmConnectionAsync(CancellationToken cancellationToken)
		{
			var inputVsize = Constants.P2wpkhInputVirtualSize;
			var inputRemainingVsizes = new[] { ProtocolConstants.MaxVsizePerAlice - inputVsize };

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
					amountsToRequest,
					cancellationToken)
				.ConfigureAwait(false);
		}

		public async Task RemoveInputAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.RemoveInputAsync(RoundId, AliceId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin, CancellationToken cancellationToken)
		{
			await ArenaClient.SignTransactionAsync(RoundId, Coin, bitcoinSecret, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}

		public static async Task<AliceClient> CreateNewAsync(
			ArenaClient arenaClient,
			Coin coin,
			BitcoinSecret bitcoinSecret,
			uint256 roundId,
			FeeRate feeRate,
			CancellationToken cancellationToken)
		{
			uint256 aliceId = await arenaClient.RegisterInputAsync(coin.Amount, coin.Outpoint, bitcoinSecret.PrivateKey, roundId, cancellationToken).ConfigureAwait(false);

			AliceClient client = new(aliceId, roundId, arenaClient, coin, feeRate);

			Logger.LogInfo($"Round ({roundId}), Alice ({aliceId}): Registered an input.");

			return client;
		}
	}
}
