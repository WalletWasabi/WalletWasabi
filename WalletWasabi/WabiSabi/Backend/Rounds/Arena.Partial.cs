using NBitcoin;
using Nito.AsyncEx;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public partial class Arena : IWabiSabiApiRequestHandler
{
	public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		var coin = await OutpointToCoinAsync(request, cancellationToken).ConfigureAwait(false);

		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var round = GetRound(request.RoundId);

			var registeredCoins = Rounds.Where(x => !(x.Phase == Phase.Ended && !x.WasTransactionBroadcast))
				.SelectMany(r => r.Alices.Select(a => a.Coin));

			if (registeredCoins.Any(x => x.Outpoint == coin.Outpoint))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);
			}

			if (round.IsInputRegistrationEnded(Config.MaxInputCountByRound))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
			}

			if (round is BlameRound blameRound && !blameRound.BlameWhitelist.Contains(coin.Outpoint))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputNotWhitelisted);
			}

			// Compute but don't commit updated CoinJoin to round state, it will
			// be re-calculated on input confirmation. This is computed it here
			// for validation purposes.
			_ = round.Assert<ConstructionState>().AddInput(coin);

			var coinJoinInputCommitmentData = new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", round.Id);
			if (!OwnershipProof.VerifyCoinJoinInputProof(request.OwnershipProof, coin.TxOut.ScriptPubKey, coinJoinInputCommitmentData))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongOwnershipProof);
			}

			// Generate a new GUID with the secure random source, to be sure
			// that it is not guessable (Guid.NewGuid() documentation does
			// not say anything about GUID version or randomness source,
			// only that the probability of duplicates is very low).
			var id = new Guid(Random.GetBytes(16));
			var alice = new Alice(coin, request.OwnershipProof, round, id);

			if (alice.TotalInputAmount < round.MinAmountCredentialValue)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (alice.TotalInputAmount > round.MaxAmountCredentialValue)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (alice.TotalInputVsize > round.MaxVsizeAllocationPerAlice)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchVsize);
			}

			var amountCredentialTask = round.AmountCredentialIssuer.HandleRequestAsync(request.ZeroAmountCredentialRequests, cancellationToken);
			var vsizeCredentialTask = round.VsizeCredentialIssuer.HandleRequestAsync(request.ZeroVsizeCredentialRequests, cancellationToken);

			if (round.RemainingInputVsizeAllocation < round.MaxVsizeAllocationPerAlice)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.VsizeQuotaExceeded);
			}

			var commitAmountCredentialResponse = await amountCredentialTask.ConfigureAwait(false);
			var commitVsizeCredentialResponse = await vsizeCredentialTask.ConfigureAwait(false);

			alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeFrame.Duration);
			round.Alices.Add(alice);

			return new(alice.Id,
				commitAmountCredentialResponse,
				commitVsizeCredentialResponse);
		}
	}

	public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken)
	{
		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var round = GetRound(request.RoundId);
			var alice = GetAlice(request.AliceId, round);
			alice.ReadyToSign = true;
		}
	}

	public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
	{
		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var round = GetRound(request.RoundId, Phase.InputRegistration);

			round.Alices.RemoveAll(x => x.Id == request.AliceId);
		}
	}

	public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
	{
		Round round;
		Alice alice;
		var realAmountCredentialRequests = request.RealAmountCredentialRequests;
		var realVsizeCredentialRequests = request.RealVsizeCredentialRequests;

		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			round = GetRound(request.RoundId, Phase.InputRegistration, Phase.ConnectionConfirmation);

			alice = GetAlice(request.AliceId, round);

			if (alice.ConfirmedConnection)
			{
				Prison.Ban(alice, round.Id);
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyConfirmedConnection, $"Round ({request.RoundId}): Alice ({request.AliceId}) already confirmed connection.");
			}

			if (realVsizeCredentialRequests.Delta != alice.CalculateRemainingVsizeCredentials(round.MaxVsizeAllocationPerAlice))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedVsizeCredentials, $"Round ({request.RoundId}): Incorrect requested vsize credentials.");
			}
			if (realAmountCredentialRequests.Delta != alice.CalculateRemainingAmountCredentials(round.FeeRate))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedAmountCredentials, $"Round ({request.RoundId}): Incorrect requested amount credentials.");
			}
		}

		var amountZeroCredentialTask = round.AmountCredentialIssuer.HandleRequestAsync(request.ZeroAmountCredentialRequests, cancellationToken);
		var vsizeZeroCredentialTask = round.VsizeCredentialIssuer.HandleRequestAsync(request.ZeroVsizeCredentialRequests, cancellationToken);
		Task<CredentialsResponse>? amountRealCredentialTask = null;
		Task<CredentialsResponse>? vsizeRealCredentialTask = null;

		if (round.Phase is Phase.ConnectionConfirmation)
		{
			amountRealCredentialTask = round.AmountCredentialIssuer.HandleRequestAsync(realAmountCredentialRequests, cancellationToken);
			vsizeRealCredentialTask = round.VsizeCredentialIssuer.HandleRequestAsync(realVsizeCredentialRequests, cancellationToken);
		}

		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			alice = GetAlice(request.AliceId, round);

			switch (round.Phase)
			{
				case Phase.InputRegistration:
					{
						var commitAmountZeroCredentialResponse = await amountZeroCredentialTask.ConfigureAwait(false);
						var commitVsizeZeroCredentialResponse = await vsizeZeroCredentialTask.ConfigureAwait(false);
						alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeFrame.Duration);
						return new(
							commitAmountZeroCredentialResponse,
							commitVsizeZeroCredentialResponse);
					}

				case Phase.ConnectionConfirmation:
					{
						// If the phase was InputRegistration before then we did not pre-calculate real credentials.
						amountRealCredentialTask ??= round.AmountCredentialIssuer.HandleRequestAsync(realAmountCredentialRequests, cancellationToken);
						vsizeRealCredentialTask ??= round.VsizeCredentialIssuer.HandleRequestAsync(realVsizeCredentialRequests, cancellationToken);

						ConnectionConfirmationResponse response = new(
							await amountZeroCredentialTask.ConfigureAwait(false),
							await vsizeZeroCredentialTask.ConfigureAwait(false),
							await amountRealCredentialTask.ConfigureAwait(false),
							await vsizeRealCredentialTask.ConfigureAwait(false));

						// Update the CoinJoin state, adding the confirmed input.
						round.CoinjoinState = round.Assert<ConstructionState>().AddInput(alice.Coin);
						alice.ConfirmedConnection = true;

						return response;
					}

				default:
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({request.RoundId}): Wrong phase ({round.Phase}).");
			}
		}
	}

	public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		return RegisterOutputCoreAsync(request, cancellationToken);
	}

	public async Task<EmptyResponse> RegisterOutputCoreAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var round = GetRound(request.RoundId, Phase.OutputRegistration);

			var credentialAmount = -request.AmountCredentialRequests.Delta;

			Bob bob = new(request.Script, credentialAmount);

			var outputValue = bob.CalculateOutputAmount(round.FeeRate);

			var vsizeCredentialRequests = request.VsizeCredentialRequests;
			if (-vsizeCredentialRequests.Delta != bob.OutputVsize)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedVsizeCredentials, $"Round ({request.RoundId}): Incorrect requested vsize credentials.");
			}

			// Update the current round state with the additional output to ensure it's valid.
			var newState = round.AddOutput(new TxOut(outputValue, bob.Script));

			// Verify the credential requests and prepare their responses.
			await round.AmountCredentialIssuer.HandleRequestAsync(request.AmountCredentialRequests, cancellationToken).ConfigureAwait(false);
			await round.VsizeCredentialIssuer.HandleRequestAsync(vsizeCredentialRequests, cancellationToken).ConfigureAwait(false);

			// Update round state.
			round.Bobs.Add(bob);
			round.CoinjoinState = newState;
		}

		return EmptyResponse.Instance;
	}

	public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
	{
		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var round = GetRound(request.RoundId, Phase.TransactionSigning);

			var state = round.Assert<SigningState>().AddWitness((int)request.InputIndex, request.Witness);

			// at this point all of the witnesses have been verified and the state can be updated
			round.CoinjoinState = state;
		}
	}

	public async Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
	{
		Round round;
		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			round = GetRound(request.RoundId, Phase.ConnectionConfirmation, Phase.OutputRegistration);
		}

		if (request.RealAmountCredentialRequests.Delta != 0)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.DeltaNotZero, $"Round ({round.Id}): Amount credentials delta must be zero.");
		}

		if (request.RealAmountCredentialRequests.Requested.Count() != ProtocolConstants.CredentialNumber)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongNumberOfCreds, $"Round ({round.Id}): Incorrect requested number of amount credentials.");
		}

		if (request.RealVsizeCredentialRequests.Requested.Count() != ProtocolConstants.CredentialNumber)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongNumberOfCreds, $"Round ({round.Id}): Incorrect requested number of weight credentials.");
		}

		var realAmountTask = round.AmountCredentialIssuer.HandleRequestAsync(request.RealAmountCredentialRequests, cancellationToken);
		var realVsizeTask = round.VsizeCredentialIssuer.HandleRequestAsync(request.RealVsizeCredentialRequests, cancellationToken);
		var zeroAmountTask = round.AmountCredentialIssuer.HandleRequestAsync(request.ZeroAmountCredentialRequests, cancellationToken);
		var zeroVsizeTask = round.VsizeCredentialIssuer.HandleRequestAsync(request.ZeroVsizeCredentialsRequests, cancellationToken);

		return new(
			await realAmountTask.ConfigureAwait(false),
			await realVsizeTask.ConfigureAwait(false),
			await zeroAmountTask.ConfigureAwait(false),
			await zeroVsizeTask.ConfigureAwait(false));
	}

	public async Task<Coin> OutpointToCoinAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		OutPoint input = request.Input;

		if (Prison.TryGet(input, out var inmate) && (!Config.AllowNotedInputRegistration || inmate.Punishment != Punishment.Noted))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputBanned);
		}

		var txOutResponse = await Rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true, cancellationToken).ConfigureAwait(false);
		if (txOutResponse is null)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputSpent);
		}
		if (txOutResponse.Confirmations == 0)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputUnconfirmed);
		}
		if (txOutResponse.IsCoinBase && txOutResponse.Confirmations <= 100)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputImmature);
		}

		return new Coin(input, txOutResponse.TxOut);
	}

	public async Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
	{
		using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return Rounds.Select(x => RoundState.FromRound(x)).ToArray();
		}
	}

	private Round GetRound(uint256 roundId) =>
		Rounds.FirstOrDefault(x => x.Id == roundId)
		?? throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({roundId}) not found.");

	private Round InPhase(Round round, Phase[] phases) =>
		phases.Contains(round.Phase)
		? round
		: throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({round.Id}): Wrong phase ({round.Phase}).");

	private Round GetRound(uint256 roundId, params Phase[] phases) =>
		InPhase(GetRound(roundId), phases);

	private Alice GetAlice(Guid aliceId, Round round) =>
		round.Alices.Find(x => x.Id == aliceId)
		?? throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({round.Id}): Alice ({aliceId}) not found.");
}
