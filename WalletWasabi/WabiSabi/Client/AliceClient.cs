using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient
	{
		public AliceClient(RoundState roundState, ArenaClient arenaClient, SmartCoin coin, BitcoinSecret bitcoinSecret)
		{
            RoundId = roundState.Id;
			ArenaClient = arenaClient;
			SmartCoin = coin;
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
		public SmartCoin SmartCoin { get; }
		private FeeRate FeeRate { get; }
		private BitcoinSecret BitcoinSecret { get; }
		public IEnumerable<Credential> IssuedAmountCredentials { get; private set; }
		public IEnumerable<Credential> IssuedVsizeCredentials { get; private set; }
		private long MaxVsizeAllocationPerAlice { get; }
		private TimeSpan ConfirmationTimeout { get; }

		public async Task RegisterAndConfirmInputAsync(RoundStateUpdater roundStatusUpdater, CancellationToken cancellationToken)
		{
			try
			{
				await RegisterInputAsync(cancellationToken).ConfigureAwait(false);
				await ConfirmConnectionAsync(roundStatusUpdater, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				await TryToUnregisterAlicesAsync(CancellationToken.None).ConfigureAwait(false);
				throw;
			}
		}

		private async Task RegisterInputAsync(CancellationToken cancellationToken)
		{
			try
			{
				var response = await ArenaClient.RegisterInputAsync(RoundId, SmartCoin.Coin.Outpoint, BitcoinSecret.PrivateKey, cancellationToken).ConfigureAwait(false);
                AliceId = response.Value;
				SmartCoin.CoinJoinInProgress = true;

				IssuedAmountCredentials = response.IssuedAmountCredentials;
				IssuedVsizeCredentials = response.IssuedVsizeCredentials;
				Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Registered {SmartCoin.OutPoint}.");
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				if (ex.InnerException is WabiSabiProtocolException wpe)
				{
					switch (wpe.ErrorCode)
					{
						case WabiSabiProtocolErrorCode.InputSpent:
							SmartCoin.SpentAccordingToBackend = true;
							Logger.LogInfo($"{SmartCoin.Coin.Outpoint} is spent according to the backend. The wallet is not fully synchronized or corrupted.");
							break;

						case WabiSabiProtocolErrorCode.InputBanned:
							SmartCoin.BannedUntilUtc = DateTimeOffset.UtcNow.AddDays(1);
							SmartCoin.SetIsBanned();
							Logger.LogInfo($"{SmartCoin.Coin.Outpoint} is banned.");
							break;

						case WabiSabiProtocolErrorCode.InputNotWhitelisted:
							SmartCoin.SpentAccordingToBackend = false;
							Logger.LogWarning($"{SmartCoin.Coin.Outpoint} cannot be registered in the blame round.");
							break;

						case WabiSabiProtocolErrorCode.AliceAlreadyRegistered:
							Logger.LogInfo($"{SmartCoin.Coin.Outpoint} was already registered.");
							break;
					}
				}
				throw;
			}
		}

		private async Task ConfirmConnectionAsync(RoundStateUpdater roundStatusUpdater, CancellationToken cancellationToken)
		{
			long[] amountsToRequest = { SmartCoin.EffectiveValue(FeeRate).Satoshi };
			long[] vsizesToRequest = { MaxVsizeAllocationPerAlice - SmartCoin.ScriptPubKey.EstimateInputVsize() };

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
			var totalFeeToPay = FeeRate.GetFee(SmartCoin.ScriptPubKey.EstimateInputVsize());
			var totalAmount = SmartCoin.Amount;
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

		private async Task TryToUnregisterAlicesAsync(CancellationToken cancellationToken)
		{
			try
			{
				await RemoveInputAsync(cancellationToken).ConfigureAwait(false);
				SmartCoin.CoinJoinInProgress = false;
				Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Unregistered {SmartCoin.OutPoint}.");
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				if (ex.InnerException is WabiSabiProtocolException wpe)
				{
					switch (wpe.ErrorCode)
					{
						case WabiSabiProtocolErrorCode.RoundNotFound:
							SmartCoin.CoinJoinInProgress = false;
							Logger.LogInfo($"{SmartCoin.Coin.Outpoint} the round was not found. Nothing to unregister.");
							break;
						case WabiSabiProtocolErrorCode.WrongPhase:
							Logger.LogInfo($"{SmartCoin.Coin.Outpoint} could not be unregistered at this phase (too late).");
							break;
					}
				}

				// Log and swallow the exception because there is nothing else that can be done here.
				Logger.LogWarning($"{SmartCoin.Coin.Outpoint} unregistration failed with {ex}.");
			}
		}

		public async Task RemoveInputAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.RemoveInputAsync(RoundId, (Guid)AliceId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(Transaction unsignedCoinJoin, CancellationToken cancellationToken)
		{
			await ArenaClient.SignTransactionAsync(RoundId, SmartCoin.Coin, BitcoinSecret, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}

		public async Task ReadyToSignAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.ReadyToSignAsync(RoundId, (Guid)AliceId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Ready to sign.");
		}
	}
}
