using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class Round
{
	private uint256 _id;

	public Round(RoundParameters roundParameters)
	{
		RoundParameters = roundParameters;

		var allowedAmounts = new MoneyRange(roundParameters.MinRegistrableAmount, RoundParameters.MaxRegistrableAmount);
		var txParams = new MultipartyTransactionParameters(roundParameters.FeeRate, roundParameters.CoordinationFeeRate, allowedAmounts, allowedAmounts, roundParameters.Network);
		CoinjoinState = new ConstructionState(txParams);

		InitialInputVsizeAllocation = CoinjoinState.Parameters.MaxTransactionSize - MultipartyTransactionParameters.SharedOverhead;
		MaxVsizeCredentialValue = Math.Min(InitialInputVsizeAllocation / RoundParameters.MaxInputCountByRound, (int)ProtocolConstants.MaxVsizeCredentialValue);
		MaxVsizeAllocationPerAlice = MaxVsizeCredentialValue;

		AmountCredentialIssuer = new(new(RoundParameters.Random), RoundParameters.Random, MaxAmountCredentialValue);
		VsizeCredentialIssuer = new(new(RoundParameters.Random), RoundParameters.Random, MaxVsizeCredentialValue);
		AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
		VsizeCredentialIssuerParameters = VsizeCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

		InputRegistrationTimeFrame = TimeFrame.Create(RoundParameters.StandardInputRegistrationTimeout).StartNow();
		ConnectionConfirmationTimeFrame = TimeFrame.Create(RoundParameters.ConnectionConfirmationTimeout);
		OutputRegistrationTimeFrame = TimeFrame.Create(RoundParameters.OutputRegistrationTimeout);
		TransactionSigningTimeFrame = TimeFrame.Create(RoundParameters.TransactionSigningTimeout);
	}

	public uint256 Id => _id ??= CalculateHash();
	public MultipartyTransactionState CoinjoinState { get; set; }
	public Network Network => RoundParameters.Network;
	public Money MinAmountCredentialValue => RoundParameters.MinRegistrableAmount;
	public Money MaxAmountCredentialValue => RoundParameters.MaxRegistrableAmount;
	public int MaxVsizeCredentialValue { get; }
	public int MaxVsizeAllocationPerAlice { get; internal set; }
	public FeeRate FeeRate => RoundParameters.FeeRate;
	public CoordinationFeeRate CoordinationFeeRate => RoundParameters.CoordinationFeeRate;
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
	public TimeFrame OutputRegistrationTimeFrame { get; private set; }
	public TimeFrame TransactionSigningTimeFrame { get; private set; }
	public DateTimeOffset End { get; private set; }
	public bool WasTransactionBroadcast { get; set; }
	public int InitialInputVsizeAllocation { get; internal set; }
	public int RemainingInputVsizeAllocation => InitialInputVsizeAllocation - (InputCount * MaxVsizeAllocationPerAlice);

	protected RoundParameters RoundParameters { get; }

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
				CoinjoinState.Parameters.AllowedInputAmounts,
				CoinjoinState.Parameters.AllowedInputTypes,
				CoinjoinState.Parameters.AllowedOutputAmounts,
				CoinjoinState.Parameters.AllowedOutputTypes,
				CoinjoinState.Parameters.Network,
				CoinjoinState.Parameters.FeeRate.FeePerK,
				CoinjoinState.Parameters.CoordinationFeeRate,
				CoinjoinState.Parameters.MaxTransactionSize,
				CoinjoinState.Parameters.MinRelayTxFee.FeePerK,
				MaxAmountCredentialValue,
				MaxVsizeCredentialValue,
				MaxVsizeAllocationPerAlice,
				AmountCredentialIssuerParameters,
				VsizeCredentialIssuerParameters);
}
