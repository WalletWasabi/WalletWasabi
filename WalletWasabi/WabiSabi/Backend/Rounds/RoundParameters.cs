using System.Collections.Immutable;
using NBitcoin;
using NBitcoin.Policy;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public record RoundParameters
{
	public static ImmutableSortedSet<ScriptType> OnlyP2WPKH = ImmutableSortedSet.Create(ScriptType.P2WPKH);

	public RoundParameters(
		Network network,
		FeeRate miningFeeRate,
		CoordinationFeeRate coordinationFeeRate,
		Money maxSuggestedAmount,
		int minInputCountByRound,
		int maxInputCountByRound,
		MoneyRange allowedInputAmounts,
		MoneyRange allowedOutputAmounts,
		TimeSpan standardInputRegistrationTimeout,
		TimeSpan connectionConfirmationTimeout,
		TimeSpan outputRegistrationTimeout,
		TimeSpan transactionSigningTimeout,
		TimeSpan blameInputRegistrationTimeout)
	{
		Network = network;
		MiningFeeRate = miningFeeRate;
		CoordinationFeeRate = coordinationFeeRate;
		MaxSuggestedAmount = maxSuggestedAmount;
		MinInputCountByRound = minInputCountByRound;
		MaxInputCountByRound = maxInputCountByRound;
		AllowedInputAmounts = allowedInputAmounts;
		AllowedOutputAmounts = allowedOutputAmounts;
		StandardInputRegistrationTimeout = standardInputRegistrationTimeout;
		ConnectionConfirmationTimeout = connectionConfirmationTimeout;
		OutputRegistrationTimeout = outputRegistrationTimeout;
		TransactionSigningTimeout = transactionSigningTimeout;
		BlameInputRegistrationTimeout = blameInputRegistrationTimeout;

		InitialInputVsizeAllocation = MaxTransactionSize - MultipartyTransactionParameters.SharedOverhead;
		MaxVsizeCredentialValue = Math.Min(InitialInputVsizeAllocation / MaxInputCountByRound, (int)ProtocolConstants.MaxVsizeCredentialValue);
		MaxVsizeAllocationPerAlice = MaxVsizeCredentialValue;
	}

	public Network Network { get; init; }
	public FeeRate MiningFeeRate { get; init; }
	public CoordinationFeeRate CoordinationFeeRate { get; init; }
	public Money MaxSuggestedAmount { get; init; }
	public int MinInputCountByRound { get; init; }
	public int MaxInputCountByRound { get; init; }
	public MoneyRange AllowedInputAmounts { get; init; }
	public MoneyRange AllowedOutputAmounts { get; init; }
	public TimeSpan StandardInputRegistrationTimeout { get; init; }
	public TimeSpan ConnectionConfirmationTimeout { get; init; }
	public TimeSpan OutputRegistrationTimeout { get; init; }
	public TimeSpan TransactionSigningTimeout { get; init; }
	public TimeSpan BlameInputRegistrationTimeout { get; init; }

	public ImmutableSortedSet<ScriptType> AllowedInputTypes { get; init; } = OnlyP2WPKH;
	public ImmutableSortedSet<ScriptType> AllowedOutputTypes { get; init; } = OnlyP2WPKH;

	public Money MinAmountCredentialValue => AllowedInputAmounts.Min;
	public Money MaxAmountCredentialValue => AllowedInputAmounts.Max;

	public int InitialInputVsizeAllocation { get; init; }
	public int MaxVsizeCredentialValue { get; init; }
	public int MaxVsizeAllocationPerAlice { get; init; }

	private static StandardTransactionPolicy StandardTransactionPolicy { get; } = new();

	// Limitation of 100kb maximum transaction size had been changed as a function of transaction weight
	// (MAX_STANDARD_TX_WEIGHT = 400000); but NBitcoin still enforces it as before.
	// Anyway, it really doesn't matter for us as it is a reasonable limit so, it doesn't affect us
	// negatively in any way.
	public int MaxTransactionSize { get; init; } = StandardTransactionPolicy.MaxTransactionSize ?? 100_000;
	public FeeRate MinRelayTxFee { get; init; } = StandardTransactionPolicy.MinRelayTxFee
												  ?? new FeeRate(Money.Satoshis(1000));

	public static RoundParameters Create(
		WabiSabiConfig wabiSabiConfig,
		Network network,
		FeeRate miningFeeRate,
		CoordinationFeeRate coordinationFeeRate,
		Money maxSuggestedAmount)
	{
		return new RoundParameters(
			network,
			miningFeeRate,
			coordinationFeeRate,
			maxSuggestedAmount,
			wabiSabiConfig.MinInputCountByRound,
			wabiSabiConfig.MaxInputCountByRound,
			new MoneyRange(wabiSabiConfig.MinRegistrableAmount, wabiSabiConfig.MaxRegistrableAmount),
			new MoneyRange(wabiSabiConfig.MinRegistrableAmount, wabiSabiConfig.MaxRegistrableAmount),
			wabiSabiConfig.StandardInputRegistrationTimeout,
			wabiSabiConfig.ConnectionConfirmationTimeout,
			wabiSabiConfig.OutputRegistrationTimeout,
			wabiSabiConfig.TransactionSigningTimeout,
			wabiSabiConfig.BlameInputRegistrationTimeout);
	}

	public Transaction CreateTransaction()
		=> Transaction.Create(Network);
}
