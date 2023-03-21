using NBitcoin;
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
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using System.Linq;
using WalletWasabi.Extensions;
using System.Net.Http;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Client;

public class AliceClient
{
	private AliceClient(
		Guid aliceId,
		RoundState roundState,
		ArenaClient arenaClient,
		SmartCoin coin,
		OwnershipProof ownershipProof,
		IEnumerable<Credential> issuedAmountCredentials,
		IEnumerable<Credential> issuedVsizeCredentials,
		bool isCoordinationFeeExempted)
	{
		var roundParameters = roundState.CoinjoinState.Parameters;
		AliceId = aliceId;
		RoundId = roundState.Id;
		ArenaClient = arenaClient;
		SmartCoin = coin;
		OwnershipProof = ownershipProof;
		FeeRate = roundParameters.MiningFeeRate;
		CoordinationFeeRate = roundParameters.CoordinationFeeRate;
		IssuedAmountCredentials = issuedAmountCredentials;
		IssuedVsizeCredentials = issuedVsizeCredentials;
		MaxVsizeAllocationPerAlice = roundParameters.MaxVsizeAllocationPerAlice;
		ConfirmationTimeout = roundParameters.ConnectionConfirmationTimeout / 2;
		IsCoordinationFeeExempted = isCoordinationFeeExempted;
	}

	public Guid AliceId { get; }
	public uint256 RoundId { get; }
	private ArenaClient ArenaClient { get; }
	public SmartCoin SmartCoin { get; }
	private OwnershipProof OwnershipProof { get; }
	private FeeRate FeeRate { get; }
	private CoordinationFeeRate CoordinationFeeRate { get; }
	public IEnumerable<Credential> IssuedAmountCredentials { get; private set; }
	public IEnumerable<Credential> IssuedVsizeCredentials { get; private set; }
	private long MaxVsizeAllocationPerAlice { get; }
	private TimeSpan ConfirmationTimeout { get; }
	public bool IsCoordinationFeeExempted { get; }

	public static async Task<AliceClient> CreateRegisterAndConfirmInputAsync(
		RoundState roundState,
		ArenaClient arenaClient,
		SmartCoin coin,
		IKeyChain keyChain,
		RoundStateUpdater roundStatusUpdater,
		CancellationToken unregisterCancellationToken,
		CancellationToken registrationCancellationToken,
		CancellationToken confirmationCancellationToken)
	{
		AliceClient? aliceClient = null;
		try
		{
			aliceClient = await RegisterInputAsync(roundState, arenaClient, coin, keyChain, registrationCancellationToken).ConfigureAwait(false);
			await aliceClient.ConfirmConnectionAsync(roundStatusUpdater, confirmationCancellationToken).ConfigureAwait(false);

			Logger.LogInfo($"Round ({aliceClient.RoundId}), Alice ({aliceClient.AliceId}): Connection was confirmed.");
		}
		catch (Exception e) when (e is OperationCanceledException || (e is AggregateException ae && ae.InnerExceptions.Last() is OperationCanceledException))
		{
			if (aliceClient is { })
			{
				// Unregistering coins is only possible before connection confirmation phase.
				await aliceClient.TryToUnregisterAlicesAsync(unregisterCancellationToken).ConfigureAwait(false);
			}

			throw;
		}

		return aliceClient;
	}

	private static async Task<AliceClient> RegisterInputAsync(RoundState roundState, ArenaClient arenaClient, SmartCoin coin, IKeyChain keyChain, CancellationToken cancellationToken)
	{
		AliceClient? aliceClient;
		try
		{
			var ownershipProof = keyChain.GetOwnershipProof(
				coin,
				new CoinJoinInputCommitmentData(arenaClient.CoordinatorIdentifier, roundState.Id));

			var (response, isCoordinationFeeExempted) = await arenaClient.RegisterInputAsync(roundState.Id, coin.Coin.Outpoint, ownershipProof, cancellationToken).ConfigureAwait(false);
			aliceClient = new(response.Value, roundState, arenaClient, coin, ownershipProof, response.IssuedAmountCredentials, response.IssuedVsizeCredentials, isCoordinationFeeExempted);
			coin.CoinJoinInProgress = true;

			Logger.LogInfo($"Round ({roundState.Id}), Alice ({aliceClient.AliceId}): Registered {coin.Outpoint}.");
		}
		catch (WabiSabiProtocolException wpe)
		{
			switch (wpe.ErrorCode)
			{
				case WabiSabiProtocolErrorCode.InputSpent:
					coin.SpentAccordingToBackend = true;
					Logger.LogInfo($"{coin.Coin.Outpoint} is spent according to the backend. The wallet is not fully synchronized or corrupted.");
					break;

				case WabiSabiProtocolErrorCode.InputBanned or WabiSabiProtocolErrorCode.InputLongBanned:
					var inputBannedExData = wpe.ExceptionData as InputBannedExceptionData;
					if (inputBannedExData is null)
					{
						Logger.LogError($"{nameof(InputBannedExceptionData)} is missing.");
					}
					coin.BannedUntilUtc = inputBannedExData?.BannedUntil ?? DateTimeOffset.UtcNow + TimeSpan.FromDays(1);
					Logger.LogInfo($"{coin.Coin.Outpoint} is banned until {coin.BannedUntilUtc}.");
					break;

				case WabiSabiProtocolErrorCode.InputNotWhitelisted:
					coin.SpentAccordingToBackend = false;
					Logger.LogWarning($"{coin.Coin.Outpoint} cannot be registered in the blame round.");
					break;

				case WabiSabiProtocolErrorCode.AliceAlreadyRegistered:
					Logger.LogInfo($"{coin.Coin.Outpoint} was already registered.");
					break;

				case WabiSabiProtocolErrorCode.WrongPhase:
					Logger.LogInfo($"{coin.Coin.Outpoint} arrived too late. Abort the rest of the registrations.");
					break;

				case WabiSabiProtocolErrorCode.RoundNotFound:
					Logger.LogInfo($"{coin.Coin.Outpoint} arrived too late because the round doesn't exist anymore. Abort the rest of the registrations.");
					break;

				default:
					Logger.LogInfo($"{coin.Coin.Outpoint} cannot be registered: '{wpe.ErrorCode}'.");
					break;
			}

			throw;
		}

		return aliceClient;
	}

	private async Task ConfirmConnectionAsync(RoundStateUpdater roundStatusUpdater, CancellationToken cancellationToken)
	{
		long[] amountsToRequest = { EffectiveValue.Satoshi };
		long[] vsizesToRequest = { MaxVsizeAllocationPerAlice - SmartCoin.ScriptPubKey.EstimateInputVsize() };

		do
		{
			using CancellationTokenSource timeout = new(ConfirmationTimeout);
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

			try
			{
				await roundStatusUpdater
					.CreateRoundAwaiterAsync(
						RoundId,
						Phase.ConnectionConfirmation,
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
		var response = await ArenaClient
			.ConfirmConnectionAsync(
				RoundId,
				AliceId,
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

	public async Task TryToUnregisterAlicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

			await RemoveInputAsync(linkedCts.Token).ConfigureAwait(false);
			SmartCoin.CoinJoinInProgress = false;
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Unregistered {SmartCoin.Outpoint}.");
		}
		catch (Exception e) when (e is OperationCanceledException || (e is AggregateException ae && ae.InnerExceptions.Last() is OperationCanceledException))
		{
			Logger.LogTrace(e);
		}
		catch (Exception e)
		{
			if (e is HttpRequestException && e.InnerException is WabiSabiProtocolException wpe)
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
			Logger.LogWarning($"{SmartCoin.Coin.Outpoint} unregistration failed with {e}.");
		}
	}

	public void Finish()
	{
		SmartCoin.CoinJoinInProgress = false;
	}

	public async Task RemoveInputAsync(CancellationToken cancellationToken)
	{
		await ArenaClient.RemoveInputAsync(RoundId, AliceId, cancellationToken).ConfigureAwait(false);
		SmartCoin.CoinJoinInProgress = false;
		Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
	}

	public async Task SignTransactionAsync(TransactionWithPrecomputedData unsignedCoinJoin, IKeyChain keyChain, CancellationToken cancellationToken)
	{
		await ArenaClient.SignTransactionAsync(RoundId, SmartCoin.Coin, keyChain, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

		Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
	}

	public async Task ReadyToSignAsync(CancellationToken cancellationToken)
	{
		await ArenaClient.ReadyToSignAsync(RoundId, AliceId, cancellationToken).ConfigureAwait(false);
		Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Ready to sign.");
	}

	public Money EffectiveValue => SmartCoin.EffectiveValue(FeeRate, IsCoordinationFeeExempted ? CoordinationFeeRate.Zero : CoordinationFeeRate);
}
