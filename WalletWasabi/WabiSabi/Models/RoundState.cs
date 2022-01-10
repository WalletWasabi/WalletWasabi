using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models;

public record RoundState(
	uint256 BlameOf,
	CredentialIssuerParameters AmountCredentialIssuerParameters,
	CredentialIssuerParameters VsizeCredentialIssuerParameters,
	FeeRate FeeRate,
	Phase Phase,
	bool WasTransactionBroadcast,
	DateTimeOffset InputRegistrationStart,
	TimeSpan InputRegistrationTimeout,
	TimeSpan ConnectionConfirmationTimeout,
	TimeSpan OutputRegistrationTimeout,
	TimeSpan TransactionSigningTimeout,
	long MaxAmountCredentialValue,
	long MaxVsizeCredentialValue,
	long MaxVsizeAllocationPerAlice,
	MultipartyTransactionState CoinjoinState)
{
	private uint256 _id;

	public uint256 Id => _id ??= CalculateHash();

	public DateTimeOffset InputRegistrationEnd => InputRegistrationStart + InputRegistrationTimeout;

	public static RoundState FromRound(Round round) =>
		new(
			round is BlameRound blameRound ? blameRound.BlameOf.Id : uint256.Zero,
			round.AmountCredentialIssuerParameters,
			round.VsizeCredentialIssuerParameters,
			round.FeeRate,
			round.Phase,
			round.WasTransactionBroadcast,
			round.InputRegistrationTimeFrame.StartTime,
			round.InputRegistrationTimeFrame.Duration,
			round.ConnectionConfirmationTimeFrame.Duration,
			round.OutputRegistrationTimeFrame.Duration,
			round.TransactionSigningTimeFrame.Duration,
			round.MaxAmountCredentialValue,
			round.MaxVsizeCredentialValue,
			round.MaxVsizeAllocationPerAlice,
			round.CoinjoinState);

	public TState Assert<TState>() where TState : MultipartyTransactionState =>
		CoinjoinState switch
		{
			TState s => s,
			_ => throw new InvalidOperationException($"{typeof(TState).Name} state was expected but {CoinjoinState.GetType().Name} state was received.")
		};

	public WabiSabiClient CreateAmountCredentialClient(WasabiRandom random) =>
		new(AmountCredentialIssuerParameters, random, MaxAmountCredentialValue);

	public WabiSabiClient CreateVsizeCredentialClient(WasabiRandom random) =>
		new(VsizeCredentialIssuerParameters, random, MaxVsizeCredentialValue);

	private uint256 CalculateHash() =>
		RoundHasher.CalculateHash(
			InputRegistrationStart,
			InputRegistrationTimeout,
			ConnectionConfirmationTimeout,
			OutputRegistrationTimeout,
			TransactionSigningTimeout,
			CoinjoinState.Parameters.AllowedInputAmounts,
			CoinjoinState.Parameters.AllowedInputTypes,
			CoinjoinState.Parameters.AllowedOutputAmounts,
			CoinjoinState.Parameters.AllowedOutputTypes,
			CoinjoinState.Parameters.Network,
			CoinjoinState.Parameters.FeeRate.FeePerK,
			CoinjoinState.Parameters.MaxTransactionSize,
			CoinjoinState.Parameters.MinRelayTxFee.FeePerK,
			MaxAmountCredentialValue,
			MaxVsizeCredentialValue,
			MaxVsizeAllocationPerAlice,
			AmountCredentialIssuerParameters,
			VsizeCredentialIssuerParameters);
}
