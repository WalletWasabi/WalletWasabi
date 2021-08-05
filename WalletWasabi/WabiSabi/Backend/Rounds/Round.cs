using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Round
	{
		public Round(RoundParameters roundParameters)
		{
			RoundParameters = roundParameters;

			var allowedAmounts = new MoneyRange(roundParameters.MinRegistrableAmount, RoundParameters.MaxRegistrableAmount);
			var txParams = new MultipartyTransactionParameters(roundParameters.FeeRate, allowedAmounts, allowedAmounts, roundParameters.Network);
			CoinjoinState = new ConstructionState(txParams);

			InitialInputVsizeAllocation = CoinjoinState.Parameters.MaxTransactionSize - MultipartyTransactionParameters.SharedOverhead;
			MaxRegistrableVsize = Math.Min(InitialInputVsizeAllocation / RoundParameters.MaxInputCountByRound, (int)ProtocolConstants.MaxVsizeCredentialValue);
			MaxVsizeAllocationPerAlice = MaxRegistrableVsize;

			AmountCredentialIssuer = new(new(RoundParameters.Random), RoundParameters.Random, MaxRegistrableAmount);
			VsizeCredentialIssuer = new(new(RoundParameters.Random), RoundParameters.Random, MaxRegistrableVsize);
			AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
			VsizeCredentialIssuerParameters = VsizeCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

			Id = CalculateHash();
			InputRegistrationTimeout = IsBlameRound ? roundParameters.BlameInputRegistrationTimeout : roundParameters.StandardInputRegistrationTimeout;
			MaxInputCountByRound = roundParameters.MaxInputCountByRound;
			MinInputCountByRound = roundParameters.MinInputCountByRound;
		}

		public MultipartyTransactionState CoinjoinState { get; set; }
		public uint256 Id { get; }
		public Network Network => RoundParameters.Network;
		public Money MinRegistrableAmount => RoundParameters.MinRegistrableAmount;
		public Money MaxRegistrableAmount => RoundParameters.MaxRegistrableAmount;
		public int MaxRegistrableVsize { get; }
		public int MaxVsizeAllocationPerAlice { get; internal set; }
		public FeeRate FeeRate => RoundParameters.FeeRate;
		public CredentialIssuer AmountCredentialIssuer { get; }
		public CredentialIssuer VsizeCredentialIssuer { get; }
		public CredentialIssuerParameters AmountCredentialIssuerParameters { get; }
		public CredentialIssuerParameters VsizeCredentialIssuerParameters { get; }
		public List<Alice> Alices { get; } = new();
		public int InputCount => Alices.Count;
		public List<Bob> Bobs { get; } = new();

		public Round? BlameOf => RoundParameters.BlameOf;
		public bool IsBlameRound => RoundParameters.IsBlameRound;
		public ISet<OutPoint> BlameWhitelist => RoundParameters.BlameWhitelist;

		public TimeSpan ConnectionConfirmationTimeout => RoundParameters.ConnectionConfirmationTimeout;
		public TimeSpan OutputRegistrationTimeout => RoundParameters.OutputRegistrationTimeout;
		public TimeSpan TransactionSigningTimeout => RoundParameters.TransactionSigningTimeout;

		public Phase Phase { get; private set; } = Phase.InputRegistration;
		public DateTimeOffset ConnectionConfirmationStart { get; private set; }
		public DateTimeOffset OutputRegistrationStart { get; private set; }
		public DateTimeOffset TransactionSigningStart { get; private set; }
		public DateTimeOffset End { get; private set; }
		public bool WasTransactionBroadcast { get; set; }
		public int InitialInputVsizeAllocation { get; internal set; }
		public int RemainingInputVsizeAllocation => InitialInputVsizeAllocation - InputCount * MaxVsizeAllocationPerAlice;

		private RoundParameters RoundParameters { get; }
		private TimeSpan InputRegistrationTimeout { get; }
		public int MaxInputCountByRound { get; }
		public int MinInputCountByRound { get; }

		public TState Assert<TState>() where TState : MultipartyTransactionState =>
			CoinjoinState switch
			{
				TState s => s,
				_ => throw new InvalidOperationException($"{typeof(TState).Name} state was expected but {CoinjoinState.GetType().Name} state was received.")
			};

		public void SetPhase(Phase phase)
		{
			if (!Enum.IsDefined<Phase>(phase))
			{
				throw new ArgumentException($"Invalid phase {phase}. This is a bug.", nameof(phase));
			}

			this.LogInfo($"Phase changed: {Phase} -> {phase}");
			Phase = phase;

			if (phase == Phase.ConnectionConfirmation)
			{
				ConnectionConfirmationStart = DateTimeOffset.UtcNow;
			}
			else if (phase == Phase.OutputRegistration)
			{
				OutputRegistrationStart = DateTimeOffset.UtcNow;
			}
			else if (phase == Phase.TransactionSigning)
			{
				TransactionSigningStart = DateTimeOffset.UtcNow;
			}
			else if (phase == Phase.Ended)
			{
				End = DateTimeOffset.UtcNow;
			}
		}

		public ConstructionState AddInput(Coin coin)
			=> Assert<ConstructionState>().AddInput(coin);

		public ConstructionState AddOutput(TxOut output)
			=> Assert<ConstructionState>().AddOutput(output);

		public SigningState AddWitness(int index, WitScript witness)
			=> Assert<SigningState>().AddWitness(index, witness);

		private uint256 CalculateHash()
			=> StrobeHasher.Create(ProtocolConstants.RoundStrobeDomain)
				.Append(ProtocolConstants.RoundMinRegistrableAmountStrobeLabel, MinRegistrableAmount)
				.Append(ProtocolConstants.RoundMaxRegistrableAmountStrobeLabel, MaxRegistrableAmount)
				.Append(ProtocolConstants.RoundMaxRegistrableVsizeStrobeLabel, MaxRegistrableVsize)
				.Append(ProtocolConstants.RoundMaxVsizePerAliceStrobeLabel, MaxVsizeAllocationPerAlice)
				.Append(ProtocolConstants.RoundAmountCredentialIssuerParametersStrobeLabel, AmountCredentialIssuerParameters)
				.Append(ProtocolConstants.RoundVsizeCredentialIssuerParametersStrobeLabel, VsizeCredentialIssuerParameters)
				.Append(ProtocolConstants.RoundFeeRateStrobeLabel, FeeRate.FeePerK)
				.GetHash();

		public async Task StartAsync(IRPCClient rpc, CancellationToken cancel)
		{
			// Input registration
			using CancellationTokenSource inputRegistrationTimeout = new(InputRegistrationTimeout);
			using CancellationTokenSource inputRegistrationCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel, inputRegistrationTimeout.Token);

			while (true)
			{
				//TODO: this can be replaces with SemaphoreSlim to avoid unnecessary iterations.
				await Task.Delay(500, inputRegistrationCancel.Token).ConfigureAwait(false);

				if (IsBlameRound)
				{
					if (BlameWhitelist.Count <= InputCount)
					{
						break;
					}
				}
				else if (InputCount >= MaxInputCountByRound)
				{
					break;
				}

				if (!inputRegistrationTimeout.IsCancellationRequested)
				{
					continue;
				}

				if (InputCount >= MinInputCountByRound)
				{
					var thereAreOffendingAlices = false;
					await foreach (var offendingAlices in CheckTxoSpendStatusAsync(rpc, this, cancel).WithCancellation(cancel).ConfigureAwait(false))
					{
						if (offendingAlices.Any())
						{
							thereAreOffendingAlices = true;
							Alices.RemoveAll(x => offendingAlices.Contains(x));
						}
					}

					if (!thereAreOffendingAlices)
					{
						SetPhase(Phase.ConnectionConfirmation);
						break;
					}
				}

				SetPhase(Phase.Ended);
				throw new InvalidOperationException($"Not enough inputs ({InputCount}) in {nameof(Phase.InputRegistration)} phase.");
			}

			// Connection confirmation
		}

		public InputRegistrationResponse RegisterInput(
			Coin coin,
			OwnershipProof ownershipProof,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroVsizeCredentialRequests)
		{
			if (Phase != Phase.InputRegistration)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
			}

			if (IsBlameRound && !BlameWhitelist.Contains(coin.Outpoint))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputNotWhitelisted);
			}

			// Compute but don't commit updated CoinJoin to round state, it will
			// be re-calculated on input confirmation. This is computed it here
			// for validation purposes.
			Assert<ConstructionState>().AddInput(coin);

			var coinJoinInputCommitmentData = new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", Id);
			if (!OwnershipProof.VerifyCoinJoinInputProof(ownershipProof, coin.TxOut.ScriptPubKey, coinJoinInputCommitmentData))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongOwnershipProof);
			}

			var alice = new Alice(coin, ownershipProof, this);

			if (alice.TotalInputAmount < MinRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (alice.TotalInputAmount > MaxRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (alice.TotalInputVsize > MaxVsizeAllocationPerAlice)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchVsize);
			}

			if (RemainingInputVsizeAllocation < MaxVsizeAllocationPerAlice)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.VsizeQuotaExceeded);
			}
			var commitAmountCredentialResponse = AmountCredentialIssuer.PrepareResponse(zeroAmountCredentialRequests);
			var commitVsizeCredentialResponse = VsizeCredentialIssuer.PrepareResponse(zeroVsizeCredentialRequests);

			alice.SetDeadlineRelativeTo(ConnectionConfirmationTimeout);
			Alices.Add(alice);

			return new(alice.Id,
				commitAmountCredentialResponse.Commit(),
				commitVsizeCredentialResponse.Commit());
		}

		private static async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(IRPCClient rpc, Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			foreach (var chunckOfAlices in round.Alices.ToList().ChunkBy(16))
			{
				var batchedRpc = rpc.PrepareBatch();

				var aliceCheckingTaskPairs = chunckOfAlices
					.Select(x => (Alice: x, StatusTask: rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true)))
					.ToList();

				cancellationToken.ThrowIfCancellationRequested();
				await batchedRpc.SendBatchAsync().ConfigureAwait(false);

				var spendStatusCheckingTasks = aliceCheckingTaskPairs.Select(async x => (x.Alice, Status: await x.StatusTask.ConfigureAwait(false)));
				var alices = await Task.WhenAll(spendStatusCheckingTasks).ConfigureAwait(false);
				yield return alices.Where(x => x.Status is null).Select(x => x.Alice).ToArray();
			}
		}
	}
}
