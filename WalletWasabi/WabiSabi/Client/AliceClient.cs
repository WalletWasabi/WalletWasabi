using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient
	{
		public AliceClient(uint256 roundId, ArenaClient arenaClient, Coin coin, FeeRate feeRate, BitcoinSecret bitcoinSecret)
		{
			AliceId = CalculateHash(coin, bitcoinSecret, roundId);
			RoundId = roundId;
			ArenaClient = arenaClient;
			Coin = coin;
			FeeRate = feeRate;
			BitcoinSecret = bitcoinSecret;
		}

		public uint256 AliceId { get; }
		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private Coin Coin { get; }
		private FeeRate FeeRate { get; }
		private BitcoinSecret BitcoinSecret { get; }

		public async Task RegisterInputAsync(CancellationToken cancellationToken)
		{
			uint256 aliceId = await ArenaClient.RegisterInputAsync(Coin.Amount, Coin.Outpoint, BitcoinSecret.PrivateKey, RoundId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({aliceId}): Registered an input.");
		}

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

		private static uint256 CalculateHash(Coin coin, BitcoinSecret bitcoinSecret, uint256 roundId)
		{
			var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
				bitcoinSecret.PrivateKey,
				new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundId));
			return new Alice(coin, ownershipProof).Id;
		}
	}
}
