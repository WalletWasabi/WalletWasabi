using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.ZeroKnowledge;
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
			RealAmountCredentials = Array.Empty<Credential>();
			RealVsizeCredentials = Array.Empty<Credential>();
		}

		public uint256 AliceId { get; }
		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		public Coin Coin { get; }
		private FeeRate FeeRate { get; }
		private BitcoinSecret BitcoinSecret { get; }
		public Credential[] RealAmountCredentials { get; private set; }
		public Credential[] RealVsizeCredentials { get; private set; }

		public async Task RegisterInputAsync(CancellationToken cancellationToken)
		{
			var response = await ArenaClient.RegisterInputAsync(RoundId, Coin.Outpoint, BitcoinSecret.PrivateKey, cancellationToken).ConfigureAwait(false);
			var remoteAliceId = response.Value;
			if (AliceId != remoteAliceId)
			{
				throw new InvalidOperationException($"Round ({RoundId}), Local Alice ({AliceId}) was computed as {remoteAliceId}");
			}
			RealAmountCredentials = response.RealAmountCredentials;
			RealVsizeCredentials = response.RealVsizeCredentials;
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Registered an input.");
		}

		public async Task ConfirmConnectionAsync(TimeSpan connectionConfirmationTimeout, IEnumerable<long> amountsToRequest, IEnumerable<long> vsizesToRequest, CancellationToken cancellationToken)
		{
			while (!await TryConfirmConnectionAsync(amountsToRequest, vsizesToRequest, cancellationToken).ConfigureAwait(false))
			{
				await Task.Delay(connectionConfirmationTimeout / 2, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task<bool> TryConfirmConnectionAsync(IEnumerable<long> amountsToRequest, IEnumerable<long> vsizesToRequest, CancellationToken cancellationToken)
		{
			if (Coin.EffectiveValue(FeeRate) <= Money.Zero)
			{
				throw new InvalidOperationException($"Round({ RoundId }), Alice({ AliceId}): Adding this input is uneconomical.");
			}

			var response = await ArenaClient
				.ConfirmConnectionAsync(
					RoundId,
					AliceId,
					amountsToRequest,
					vsizesToRequest,
					RealAmountCredentials,
					RealVsizeCredentials,
					cancellationToken)
				.ConfigureAwait(false);

			var isConfirmed = response.Value;
			if (isConfirmed)
			{
				RealAmountCredentials = response.RealAmountCredentials;
				RealVsizeCredentials = response.RealVsizeCredentials;
			}
			return isConfirmed;
		}

		public async Task RemoveInputAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.RemoveInputAsync(RoundId, AliceId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(Transaction unsignedCoinJoin, CancellationToken cancellationToken)
		{
			await ArenaClient.SignTransactionAsync(RoundId, Coin, BitcoinSecret, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}

		public async Task ReadyToSignAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.ReadyToSignAsync(RoundId, AliceId, BitcoinSecret.PrivateKey, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Ready to sign.");
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
