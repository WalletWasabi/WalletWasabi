using System.Collections.Immutable;
using NBitcoin;
using NBitcoin.Policy;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Coordinator;

namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public record RoundParameters
{
	public RoundParameters(
		Network network,
		FeeRate miningFeeRate,
		Money maxSuggestedAmount,
		int minInputCountByRound,
		int maxInputCountByRound,
		MoneyRange allowedInputAmounts,
		MoneyRange allowedOutputAmounts,
		ImmutableSortedSet<ScriptType> allowedInputTypes,
		ImmutableSortedSet<ScriptType> allowedOutputTypes,
		TimeSpan standardInputRegistrationTimeout,
		TimeSpan connectionConfirmationTimeout,
		TimeSpan outputRegistrationTimeout,
		TimeSpan transactionSigningTimeout,
		TimeSpan blameInputRegistrationTimeout,
		string coordinationIdentifier,
		bool delayTransactionSigning)
	{
		Network = network;
		MiningFeeRate = miningFeeRate;
		MaxSuggestedAmount = maxSuggestedAmount;
		MinInputCountByRound = minInputCountByRound;
		MaxInputCountByRound = maxInputCountByRound;
		AllowedInputAmounts = allowedInputAmounts;
		AllowedOutputAmounts = allowedOutputAmounts;
		AllowedInputTypes = allowedInputTypes;
		AllowedOutputTypes = allowedOutputTypes;
		StandardInputRegistrationTimeout = standardInputRegistrationTimeout;
		ConnectionConfirmationTimeout = connectionConfirmationTimeout;
		OutputRegistrationTimeout = outputRegistrationTimeout;
		TransactionSigningTimeout = transactionSigningTimeout + TimeSpan.FromSeconds(delayTransactionSigning ? 50 : 0);
		BlameInputRegistrationTimeout = blameInputRegistrationTimeout;

		InitialInputVsizeAllocation = MaxTransactionSize - MultipartyTransactionParameters.SharedOverhead;
		MaxVsizeCredentialValue = Math.Min(InitialInputVsizeAllocation / MaxInputCountByRound, (int)ProtocolConstants.MaxVsizeCredentialValue);
		MaxVsizeAllocationPerAlice = MaxVsizeCredentialValue;
		CoordinationIdentifier = coordinationIdentifier;
		DelayTransactionSigning = delayTransactionSigning;
	}

	public Network Network { get; init; }
	public FeeRate MiningFeeRate { get; init; }
	public Money MaxSuggestedAmount { get; init; }
	public int MinInputCountByRound { get; init; }
	public int MaxInputCountByRound { get; init; }
	public MoneyRange AllowedInputAmounts { get; init; }
	public MoneyRange AllowedOutputAmounts { get; init; }
	public ImmutableSortedSet<ScriptType> AllowedInputTypes { get; init; }
	public ImmutableSortedSet<ScriptType> AllowedOutputTypes { get; init; }
	public TimeSpan StandardInputRegistrationTimeout { get; init; }
	public TimeSpan ConnectionConfirmationTimeout { get; init; }
	public TimeSpan OutputRegistrationTimeout { get; init; }
	public TimeSpan TransactionSigningTimeout { get; init; }
	public TimeSpan BlameInputRegistrationTimeout { get; init; }

	public Money MinAmountCredentialValue => AllowedInputAmounts.Min;
	public Money MaxAmountCredentialValue => AllowedInputAmounts.Max;

	public int InitialInputVsizeAllocation { get; init; }
	public int MaxVsizeCredentialValue { get; init; }
	public int MaxVsizeAllocationPerAlice { get; init; }

	public string CoordinationIdentifier { get; init; }

	public bool DelayTransactionSigning { get; }

	private static StandardTransactionPolicy StandardTransactionPolicy { get; } = new();

	// Limitation of 100kb maximum transaction size had been changed as a function of transaction weight
	// (MAX_STANDARD_TX_WEIGHT = 400000); but NBitcoin still enforces it as before.
	// Anyway, it really doesn't matter for us as it is a reasonable limit so, it doesn't affect us
	// negatively in any way.
	public int MaxTransactionSize { get; init; } = StandardTransactionPolicy.MaxTransactionSize ?? 100_000;
	public FeeRate MinRelayTxFee { get; init; } = StandardTransactionPolicy.MinRelayTxFee
												  ?? new FeeRate(Money.Satoshis(1000));

	public static RoundParameters Create(
		Config wabiSabiConfig,
		Network network,
		FeeRate miningFeeRate,
		Money maxSuggestedAmount)
	{
		return new RoundParameters(
			network,
			miningFeeRate,
			maxSuggestedAmount,
			wabiSabiConfig.MinInputCountByRound,
			wabiSabiConfig.MaxInputCountByRound,
			new MoneyRange(wabiSabiConfig.MinRegistrableAmount, wabiSabiConfig.MaxRegistrableAmount),
			new MoneyRange(wabiSabiConfig.MinRegistrableAmount, wabiSabiConfig.MaxRegistrableAmount),
			wabiSabiConfig.AllowedInputTypes,
			wabiSabiConfig.AllowedOutputTypes,
			wabiSabiConfig.StandardInputRegistrationTimeout,
			wabiSabiConfig.ConnectionConfirmationTimeout,
			wabiSabiConfig.OutputRegistrationTimeout,
			wabiSabiConfig.TransactionSigningTimeout,
			wabiSabiConfig.BlameInputRegistrationTimeout,
			wabiSabiConfig.CoordinatorIdentifier,
			wabiSabiConfig.DelayTransactionSigning);
	}

	public Transaction CreateTransaction()
		=> Transaction.Create(Network);
}
