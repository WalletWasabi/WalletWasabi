using NBitcoin;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using CredentialIssuerParameters = WabiSabi.Crypto.CredentialIssuerParameters;

namespace WalletWasabi.WabiSabi.Models;

public record RoundState(uint256 Id,
	uint256 BlameOf,
	CredentialIssuerParameters AmountCredentialIssuerParameters,
	CredentialIssuerParameters VsizeCredentialIssuerParameters,
	Phase Phase,
	EndRoundState EndRoundState,
	DateTimeOffset InputRegistrationStart,
	TimeSpan InputRegistrationTimeout,
	MultipartyTransactionState CoinjoinState)
{
	private readonly Lazy<uint256> _calculatedRoundId = new(() => RoundHasher.CalculateHash(
		InputRegistrationStart,
		InputRegistrationTimeout,
		CoinjoinState.Parameters.ConnectionConfirmationTimeout,
		CoinjoinState.Parameters.OutputRegistrationTimeout,
		CoinjoinState.Parameters.TransactionSigningTimeout,
		CoinjoinState.Parameters.AllowedInputAmounts,
		CoinjoinState.Parameters.AllowedInputTypes,
		CoinjoinState.Parameters.AllowedOutputAmounts,
		CoinjoinState.Parameters.AllowedOutputTypes,
		CoinjoinState.Parameters.Network,
		CoinjoinState.Parameters.MiningFeeRate.FeePerK,
		CoinjoinState.Parameters.MaxTransactionSize,
		CoinjoinState.Parameters.MinRelayTxFee.FeePerK,
		CoinjoinState.Parameters.MaxAmountCredentialValue,
		CoinjoinState.Parameters.MaxVsizeCredentialValue,
		CoinjoinState.Parameters.MaxVsizeAllocationPerAlice,
		CoinjoinState.Parameters.MaxSuggestedAmount,
		CoinjoinState.Parameters.CoordinationIdentifier,
		AmountCredentialIssuerParameters,
		VsizeCredentialIssuerParameters));

	public bool IsRoundIdMatching() => Id == _calculatedRoundId.Value;
	public DateTimeOffset InputRegistrationEnd => InputRegistrationStart + InputRegistrationTimeout;
	public bool IsBlame => BlameOf != uint256.Zero;

	public static RoundState FromRound(Round round, int stateId = 0) =>
		new(
			round.Id,
			round is BlameRound blameRound ? blameRound.BlameOf.Id : uint256.Zero,
			round.AmountCredentialIssuerParameters,
			round.VsizeCredentialIssuerParameters,
			round.Phase,
			round.EndRoundState,
			round.InputRegistrationTimeFrame.StartTime,
			round.InputRegistrationTimeFrame.Duration,
			round.CoinjoinState.GetStateFrom(stateId)
			);

	public RoundState GetSubState(int skipFromBaseState) =>
		new(
			Id,
			BlameOf,
			AmountCredentialIssuerParameters,
			VsizeCredentialIssuerParameters,
			Phase,
			EndRoundState,
			InputRegistrationStart,
			InputRegistrationTimeout,
			CoinjoinState.GetStateFrom(skipFromBaseState)
			);

	public TState Assert<TState>() where TState : MultipartyTransactionState =>
		CoinjoinState switch
		{
			TState s => s,
			_ => throw new InvalidOperationException($"{typeof(TState).Name} state was expected but {CoinjoinState.GetType().Name} state was received.")
		};

	public WabiSabiClient CreateAmountCredentialClient(WasabiRandom random) =>
		new(AmountCredentialIssuerParameters, random, CoinjoinState.Parameters.MaxAmountCredentialValue);

	public WabiSabiClient CreateVsizeCredentialClient(WasabiRandom random) =>
		new(VsizeCredentialIssuerParameters, random, CoinjoinState.Parameters.MaxVsizeCredentialValue);
}
