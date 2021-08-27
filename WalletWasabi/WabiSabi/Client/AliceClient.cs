using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient
	{
		public AliceClient(RoundState roundState, ArenaClient arenaClient, Coin coin, BitcoinSecret bitcoinSecret)
		{
			RoundId = roundState.Id;
			ArenaClient = arenaClient;
			Coin = coin;
			FeeRate = roundState.FeeRate;
			BitcoinSecret = bitcoinSecret;
			IssuedAmountCredentials = Array.Empty<Credential>();
			IssuedVsizeCredentials = Array.Empty<Credential>();
			MaxVsizeAllocationPerAlice = roundState.MaxVsizeAllocationPerAlice;
			ConfirmationTimeout = roundState.ConnectionConfirmationTimeout / 2;
		}

		public Guid? AliceId { get; private set; }
		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		public Coin Coin { get; }
		private FeeRate FeeRate { get; }
		private BitcoinSecret BitcoinSecret { get; }
		public IEnumerable<Credential> IssuedAmountCredentials { get; private set; }
		public IEnumerable<Credential> IssuedVsizeCredentials { get; private set; }
		private long MaxVsizeAllocationPerAlice { get; }
		private TimeSpan ConfirmationTimeout { get; }

		public async Task RegisterAndConfirmInputAsync(RoundStateUpdater roundStatusUpdater, CancellationToken cancellationToken)
		{
			var response = await ArenaClient.RegisterInputAsync(RoundId, Coin.Outpoint, BitcoinSecret.PrivateKey, cancellationToken).ConfigureAwait(false);
			AliceId = response.Value;

			IssuedAmountCredentials = response.IssuedAmountCredentials;
			IssuedVsizeCredentials = response.IssuedVsizeCredentials;
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Registered an input.");

			long[] amountsToRequest = { Coin.EffectiveValue(FeeRate).Satoshi };
			long[] vsizesToRequest = { MaxVsizeAllocationPerAlice - Coin.ScriptPubKey.EstimateInputVsize() };

			do
			{
				using CancellationTokenSource timeout = new(ConfirmationTimeout);
				using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

				try
				{
					await roundStatusUpdater
						.CreateRoundAwaiter(
							RoundId,
							roundState => roundState.Phase == Phase.ConnectionConfirmation,
							cts.Token)
						.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
			while (!await TryConfirmConnectionAsync(amountsToRequest, vsizesToRequest, cancellationToken).ConfigureAwait(false));
		}

		private async Task<bool> TryConfirmConnectionAsync(IEnumerable<long> amountsToRequest, IEnumerable<long> vsizesToRequest, CancellationToken cancellationToken)
		{
			var inputVsize = Coin.ScriptPubKey.EstimateInputVsize();

			var totalFeeToPay = FeeRate.GetFee(Coin.ScriptPubKey.EstimateInputVsize());
			var totalAmount = Coin.Amount;
			var effectiveAmount = totalAmount - totalFeeToPay;

			if (effectiveAmount <= Money.Zero)
			{
				throw new InvalidOperationException($"Round({ RoundId }), Alice({ AliceId}): Adding this input is uneconomical.");
			}

			var response = await ArenaClient
				.ConfirmConnectionAsync(
					RoundId,
					(Guid)AliceId,
					amountsToRequest,
					vsizesToRequest,
					IssuedAmountCredentials,
					IssuedVsizeCredentials,
					cancellationToken)
				.ConfigureAwait(false);

			IssuedAmountCredentials = response.IssuedAmountCredentials;
			IssuedVsizeCredentials = response.IssuedVsizeCredentials;

			var isConfirmed = response.Value;
			return isConfirmed;
		}

		public async Task RemoveInputAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.RemoveInputAsync(RoundId, (Guid)AliceId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(Transaction unsignedCoinJoin, CancellationToken cancellationToken)
		{
			await ArenaClient.SignTransactionAsync(RoundId, Coin, BitcoinSecret, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}

		public async Task ReadyToSignAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.ReadyToSignAsync(RoundId, (Guid)AliceId, BitcoinSecret.PrivateKey, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Ready to sign.");
		}
	}
}
