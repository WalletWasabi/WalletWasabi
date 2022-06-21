using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

/// <summary>
/// DO ONLY APPEND TO THE END
/// Otherwise serialization ruins compatibility with clients.
/// </summary>
public enum EndRoundState
{
	None,
	AbortedWithError,
	AbortedNotEnoughAlices,
	TransactionBroadcastFailed,
	TransactionBroadcasted,
	NotAllAlicesSign,
	AbortedNotEnoughAlicesSigned,
	AbortedNotAllAlicesConfirmed
}

public class Round
{
	public Round(RoundParameters parameters, WasabiRandom random)
	{
		Parameters = parameters;

		CoinjoinState = new ConstructionState(Parameters);

		AmountCredentialIssuer = new(new(random), random, Parameters.MaxAmountCredentialValue);
		VsizeCredentialIssuer = new(new(random), random, Parameters.MaxVsizeCredentialValue);
		AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
		VsizeCredentialIssuerParameters = VsizeCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

		InputRegistrationTimeFrame = TimeFrame.Create(Parameters.StandardInputRegistrationTimeout).StartNow();
		ConnectionConfirmationTimeFrame = TimeFrame.Create(Parameters.ConnectionConfirmationTimeout);
		OutputRegistrationTimeFrame = TimeFrame.Create(Parameters.OutputRegistrationTimeout);
		TransactionSigningTimeFrame = TimeFrame.Create(Parameters.TransactionSigningTimeout);

		Id = CalculateHash();
	}

	public uint256 Id { get; }
	public MultipartyTransactionState CoinjoinState { get; set; }

	public CredentialIssuer AmountCredentialIssuer { get; }
	public CredentialIssuer VsizeCredentialIssuer { get; }
	public CredentialIssuerParameters AmountCredentialIssuerParameters { get; }
	public CredentialIssuerParameters VsizeCredentialIssuerParameters { get; }
	public List<Alice> Alices { get; } = new();
	public int InputCount => Alices.Count;
	public List<Bob> Bobs { get; } = new();

	public Phase Phase { get; private set; } = Phase.InputRegistration;
	public TimeFrame InputRegistrationTimeFrame { get; internal set; }
	public TimeFrame ConnectionConfirmationTimeFrame { get; private set; }
	public TimeFrame OutputRegistrationTimeFrame { get; set; }
	public TimeFrame TransactionSigningTimeFrame { get; set; }
	public DateTimeOffset End { get; private set; }
	public EndRoundState EndRoundState { get; set; }
	public int RemainingInputVsizeAllocation => Parameters.InitialInputVsizeAllocation - (InputCount * Parameters.MaxVsizeAllocationPerAlice);

	public RoundParameters Parameters { get; }
	public Script CoordinatorScript { get; set; }

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
			ConnectionConfirmationTimeFrame = ConnectionConfirmationTimeFrame.StartNow();
		}
		else if (phase == Phase.OutputRegistration)
		{
			OutputRegistrationTimeFrame = OutputRegistrationTimeFrame.StartNow();
		}
		else if (phase == Phase.TransactionSigning)
		{
			TransactionSigningTimeFrame = TransactionSigningTimeFrame.StartNow();
		}
		else if (phase == Phase.Ended)
		{
			End = DateTimeOffset.UtcNow;
		}
	}

	public void EndRound(EndRoundState finalState)
	{
		SetPhase(Phase.Ended);
		EndRoundState = finalState;
	}

	public virtual bool IsInputRegistrationEnded(int maxInputCount)
	{
		if (Phase > Phase.InputRegistration)
		{
			return true;
		}

		if (InputCount >= maxInputCount)
		{
			return true;
		}

		return InputRegistrationTimeFrame.HasExpired;
	}

	public ConstructionState AddInput(Coin coin)
		=> Assert<ConstructionState>().AddInput(coin);

	public ConstructionState AddOutput(TxOut output)
		=> Assert<ConstructionState>().AddOutput(output);

	public SigningState AddWitness(int index, WitScript witness)
		=> Assert<SigningState>().AddWitness(index, witness);

	private uint256 CalculateHash()
		=> RoundHasher.CalculateHash(
				InputRegistrationTimeFrame.StartTime,
				InputRegistrationTimeFrame.Duration,
				ConnectionConfirmationTimeFrame.Duration,
				OutputRegistrationTimeFrame.Duration,
				TransactionSigningTimeFrame.Duration,
				Parameters.AllowedInputAmounts,
				Parameters.AllowedInputTypes,
				Parameters.AllowedOutputAmounts,
				Parameters.AllowedOutputTypes,
				Parameters.Network,
				Parameters.MiningFeeRate.FeePerK,
				Parameters.CoordinationFeeRate,
				Parameters.MaxTransactionSize,
				Parameters.MinRelayTxFee.FeePerK,
				Parameters.MaxAmountCredentialValue,
				Parameters.MaxVsizeCredentialValue,
				Parameters.MaxVsizeAllocationPerAlice,
				Parameters.MaxSuggestedAmount,
				AmountCredentialIssuerParameters,
				VsizeCredentialIssuerParameters);
}
