using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Arena : PeriodicRunner
	{
		public Arena(TimeSpan period, Network network, WabiSabiConfig config, IRPCClient rpc, Prison prison) : base(period)
		{
			Network = network;
			Config = config;
			Rpc = rpc;
			Prison = prison;
			Random = new SecureRandom();
		}

		public HashSet<Round> Rounds { get; } = new();
		private AsyncLock AsyncLock { get; } = new();
		public Network Network { get; }
		public WabiSabiConfig Config { get; }
		public IRPCClient Rpc { get; }
		public Prison Prison { get; }
		public SecureRandom Random { get; }

		public IEnumerable<Round> ActiveRounds => Rounds.Where(x => x.Phase != Phase.Ended);

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				TimeoutRounds();

				TimeoutAlices();

				await StepTransactionSigningPhaseAsync(cancel).ConfigureAwait(false);

				StepOutputRegistrationPhase();

				StepConnectionConfirmationPhase();

				await StepInputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

				cancel.ThrowIfCancellationRequested();

				// Ensure there's at least one non-blame round in input registration.
				await CreateRoundsAsync(cancel).ConfigureAwait(false);
			}
		}

		private async Task StepInputRegistrationPhaseAsync(CancellationToken cancel)
		{
			foreach (var round in Rounds.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x.IsInputRegistrationEnded(Config.MaxInputCountByRound, Config.GetInputRegistrationTimeout(x)))
				.ToArray())
			{
				if (round.InputCount < Config.MinInputCountByRound)
				{
					round.SetPhase(Phase.Ended);
					round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase.");
				}
				else
				{
					var thereAreOffendingAlices = false;
					await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
					{
						if (offendingAlices.Any())
						{
							thereAreOffendingAlices = true;
							round.Alices.RemoveAll(x => offendingAlices.Contains(x));
						}
					}
					if (!thereAreOffendingAlices)
					{
						round.SetPhase(Phase.ConnectionConfirmation);
					}
				}
			}
		}

		private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			foreach (var chunckOfAlices in round.Alices.ToList().ChunkBy(16))
			{
				var batchedRpc = Rpc.PrepareBatch();

				var aliceCheckingTaskPairs = chunckOfAlices
					.Select(x => (Alice: x, StatusTask: Rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true)))
					.ToList();

				cancellationToken.ThrowIfCancellationRequested();
				await batchedRpc.SendBatchAsync().ConfigureAwait(false);

				var spendStatusCheckingTasks = aliceCheckingTaskPairs.Select(async x => (x.Alice, Status: await x.StatusTask.ConfigureAwait(false)));
				var alices = await Task.WhenAll(spendStatusCheckingTasks).ConfigureAwait(false);
				yield return alices.Where(x => x.Status is null).Select(x => x.Alice).ToArray();
			}
		}

		private void StepConnectionConfirmationPhase()
		{
			foreach (var round in Rounds.Where(x => x.Phase == Phase.ConnectionConfirmation).ToArray())
			{
				if (round.Alices.All(x => x.ConfirmedConnection))
				{
					round.SetPhase(Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationStart + round.ConnectionConfirmationTimeout < DateTimeOffset.UtcNow)
				{
					var alicesDidntConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					foreach (var alice in alicesDidntConfirm)
					{
						Prison.Note(alice, round.Id);
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidntConfirm.Contains(x));
					round.LogInfo($"{removedAliceCount} alices removed because they didn't confirm.");

					if (round.InputCount < Config.MinInputCountByRound)
					{
						round.SetPhase(Phase.Ended);
						round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase.");
					}
					else
					{
						round.SetPhase(Phase.OutputRegistration);
					}
				}
			}
		}

		private void StepOutputRegistrationPhase()
		{
			foreach (var round in Rounds.Where(x => x.Phase == Phase.OutputRegistration).ToArray())
			{
				long aliceSum = round.Alices.Sum(x => x.CalculateRemainingAmountCredentials(round.FeeRate));
				long bobSum = round.Bobs.Sum(x => x.CredentialAmount);
				var diff = aliceSum - bobSum;
				var allReady = round.Alices.All(a => a.ReadyToSign);

				if (allReady || round.OutputRegistrationStart + round.OutputRegistrationTimeout < DateTimeOffset.UtcNow)
				{
					var coinjoin = round.Assert<ConstructionState>();

					round.LogInfo($"{coinjoin.Inputs.Count} inputs were added.");
					round.LogInfo($"{coinjoin.Outputs.Count} outputs were added.");

					// If timeout we must fill up the outputs to build a reasonable transaction.
					// This won't be signed by the alice who failed to provide output, so we know who to ban.
					var diffMoney = Money.Satoshis(diff) - coinjoin.Parameters.FeeRate.GetFee(Config.BlameScript.EstimateOutputVsize());
					if (!allReady && diffMoney > coinjoin.Parameters.AllowedOutputAmounts.Min)
					{
						coinjoin = coinjoin.AddOutput(new TxOut(diffMoney, Config.BlameScript));
						round.LogInfo("Filled up the outputs to build a reasonable transaction because some alice failed to provide its output.");
					}

					round.CoinjoinState = coinjoin.Finalize();

					round.SetPhase(Phase.TransactionSigning);
				}
			}
		}

		private async Task StepTransactionSigningPhaseAsync(CancellationToken cancellationToken)
		{
			foreach (var round in Rounds.Where(x => x.Phase == Phase.TransactionSigning).ToArray())
			{
				var state = round.Assert<SigningState>();

				try
				{
					if (state.IsFullySigned)
					{
						var coinjoin = state.CreateTransaction();

						// Logging.
						round.LogInfo("Trying to broadcast coinjoin.");
						Coin[]? spentCoins = round.Alices.Select(x => x.Coin).ToArray();
						Money networkFee = coinjoin.GetFee(spentCoins);
						uint256 roundId = round.Id;
						FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
						round.LogInfo($"Network Fee: {networkFee.ToString(false, false)} BTC.");
						round.LogInfo($"Network Fee Rate: {feeRate.FeePerK.ToDecimal(MoneyUnit.Satoshi) / 1000} sat/vByte.");
						round.LogInfo($"Number of inputs: {coinjoin.Inputs.Count}.");
						round.LogInfo($"Number of outputs: {coinjoin.Outputs.Count}.");
						round.LogInfo($"Serialized Size: {coinjoin.GetSerializedSize() / 1024} KB.");
						round.LogInfo($"VSize: {coinjoin.GetVirtualSize() / 1024} KB.");
						var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
						foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
						{
							round.LogInfo($"There are {count} occurrences of {value.ToString(true, false)} outputs.");
						}
						round.LogInfo($"There are {indistinguishableOutputs.Count(x => x.count == 1)} occurrences of unique outputs.");

						// Broadcasting.
						await Rpc.SendRawTransactionAsync(coinjoin).ConfigureAwait(false);
						round.WasTransactionBroadcast = true;
						round.SetPhase(Phase.Ended);

						round.LogInfo($"Successfully broadcast the CoinJoin: {coinjoin.GetHash()}.");
					}
					else if (round.TransactionSigningStart + round.TransactionSigningTimeout < DateTimeOffset.UtcNow)
					{
						throw new TimeoutException($"Round {round.Id}: Signing phase timed out after {round.TransactionSigningTimeout.TotalSeconds} seconds.");
					}
				}
				catch (Exception ex)
				{
					round.LogWarning($"Signing phase failed, reason: '{ex}'.");
					await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		private async Task FailTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
		{
			var state = round.Assert<SigningState>();

			var unsignedPrevouts = state.UnsignedInputs.ToHashSet();

			var alicesWhoDidntSign = round.Alices
				.Select(alice => (Alice: alice, alice.Coin))
				.Where(x => unsignedPrevouts.Contains(x.Coin))
				.Select(x => x.Alice)
				.ToHashSet();

			foreach (var alice in alicesWhoDidntSign)
			{
				Prison.Note(alice, round.Id);
			}

			round.Alices.RemoveAll(x => alicesWhoDidntSign.Contains(x));
			round.SetPhase(Phase.Ended);

			if (round.InputCount >= Config.MinInputCountByRound)
			{
				await CreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task CreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
		{
			var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true).ConfigureAwait(false)).FeeRate;
			RoundParameters parameters = new(Config, Network, Random, feeRate, blameOf: round, Prison);
			Round blameRound = new(parameters);
			Rounds.Add(blameRound);
		}

		private async Task CreateRoundsAsync(CancellationToken cancellationToken)
		{
			if (!Rounds.Any(x => !x.IsBlameRound && x.Phase == Phase.InputRegistration))
			{
				var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true).ConfigureAwait(false)).FeeRate;

				RoundParameters roundParams = new(Config, Network, Random, feeRate);
				Round r = new(roundParams);
				Rounds.Add(r);
			}
		}

		private void TimeoutRounds()
		{
			foreach (var expiredRound in Rounds.Where(
				x =>
				x.Phase == Phase.Ended
				&& x.End + Config.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
			{
				Rounds.Remove(expiredRound);
			}
		}

		private void TimeoutAlices()
		{
			foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(Config.MaxInputCountByRound, Config.GetInputRegistrationTimeout(x))).ToArray())
			{
				var removedAliceCount = round.Alices.RemoveAll(x => x.Deadline < DateTimeOffset.UtcNow);
				if (removedAliceCount > 0)
				{
					round.LogInfo($"{removedAliceCount} alices timed out and removed.");
				}
			}
		}

		public async Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
		{
			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				return Rounds.Select(x => RoundState.FromRound(x)).ToArray();
			}
		}

		public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
		{
			var coin = await OutpointToCoinAsync(request, cancellationToken).ConfigureAwait(false);

			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (Rounds.FirstOrDefault(x => x.Id == request.RoundId) is not { } round)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}

				var registeredCoins = Rounds.Where(x => !(x.Phase == Phase.Ended && !x.WasTransactionBroadcast))
					.SelectMany(r => r.Alices.Select(a => a.Coin));

				if (registeredCoins.Any(x => x.Outpoint == coin.Outpoint))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);
				}

				if (round.IsInputRegistrationEnded(
					Config.MaxInputCountByRound,
					Config.GetInputRegistrationTimeout(round)))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}

				if (round.IsBlameRound && !round.BlameWhitelist.Contains(coin.Outpoint))
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

				// Generate a new Guid with the secure random source, to be sure
				// that it is not guessable (Guid.NewGuid() documentation does
				// not say anything about GUID version or randomness source,
				// only that the probability of duplicates is very low).
				var id = new Guid(Random.GetBytes(16));
				var alice = new Alice(coin, request.OwnershipProof, round, id);

				if (alice.TotalInputAmount < round.MinRegistrableAmount)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
				}
				if (alice.TotalInputAmount > round.MaxRegistrableAmount)
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

				alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
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
				if (Rounds.FirstOrDefault(r => r.Id == request.RoundId) is not Round round)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
				}

				if (round.Alices.FirstOrDefault(a => a.Id == request.AliceId) is not Alice alice)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice id ({request.AliceId}).");
				}

				alice.ReadyToSign = true;
			}
		}

		public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
		{
			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (Rounds.FirstOrDefault(x => x.Id == request.RoundId) is not Round round)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
				}
				if (round.Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({request.RoundId}): Wrong phase ({round.Phase}).");
				}
				round.Alices.RemoveAll(x => x.Id == request.AliceId);
			}
		}

		public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
		{
			Round? round;
			Alice? alice;
			var realAmountCredentialRequests = request.RealAmountCredentialRequests;
			var realVsizeCredentialRequests = request.RealVsizeCredentialRequests;

			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				round = Rounds.FirstOrDefault(x => x.Id == request.RoundId);
				if (round is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
				}

				if (round.Phase is not (Phase.InputRegistration or Phase.ConnectionConfirmation))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({request.RoundId}): Wrong phase ({round.Phase}).");
				}

				alice = round.Alices.FirstOrDefault(x => x.Id == request.AliceId);
				if (alice is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
				}

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
				if (round.Alices.FirstOrDefault(x => x.Id == request.AliceId) != alice)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
				}

				switch (round.Phase)
				{
					case Phase.InputRegistration:
						{
							var commitAmountZeroCredentialResponse = await amountZeroCredentialTask.ConfigureAwait(false);
							var commitVsizeZeroCredentialResponse = await vsizeZeroCredentialTask.ConfigureAwait(false);
							alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
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

		public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
		{
			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (Rounds.FirstOrDefault(x => x.Id == request.RoundId) is not Round round)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
				}

				var credentialAmount = -request.AmountCredentialRequests.Delta;

				Bob bob = new(request.Script, credentialAmount);

				var outputValue = bob.CalculateOutputAmount(round.FeeRate);

				var vsizeCredentialRequests = request.VsizeCredentialRequests;
				if (-vsizeCredentialRequests.Delta != bob.OutputVsize)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedVsizeCredentials, $"Round ({request.RoundId}): Incorrect requested vsize credentials.");
				}

				if (round.Phase != Phase.OutputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({request.RoundId}): Wrong phase ({round.Phase}).");
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
		}

		public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
		{
			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (Rounds.FirstOrDefault(x => x.Id == request.RoundId) is not Round round)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
				}

				if (round.Phase != Phase.TransactionSigning)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({request.RoundId}): Wrong phase ({round.Phase}).");
				}

				var state = round.Assert<SigningState>();
				foreach (var inputWitnessPair in request.InputWitnessPairs)
				{
					state = state.AddWitness((int)inputWitnessPair.InputIndex, inputWitnessPair.Witness);
				}

				// at this point all of the witnesses have been verified and the state can be updated
				round.CoinjoinState = state;
			}
		}

		public async Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
		{
			Round? round;
			using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				round = Rounds.FirstOrDefault(x => x.Id == request.RoundId);
				if (round is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
				}

				if (round.Phase is not (Phase.ConnectionConfirmation or Phase.OutputRegistration))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({round.Id}): Wrong phase ({round.Phase}).");
				}
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

			var txOutResponse = await Rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true).ConfigureAwait(false);
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

		public override void Dispose()
		{
			Random.Dispose();
			base.Dispose();
		}
	}
}
