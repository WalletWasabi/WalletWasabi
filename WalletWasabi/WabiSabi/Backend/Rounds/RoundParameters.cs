using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using NBitcoin.Policy;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public record RoundParameters
{
	public static readonly ImmutableSortedSet<ScriptType> OnlyP2WPKH = ImmutableSortedSet.Create(ScriptType.P2WPKH);

	public RoundParameters(
		Network network,
		FeeRate miningFeeRate,
		CoordinationFeeRate coordinationFeeRate,
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
		CoordinationFeeRate = coordinationFeeRate;
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
	public CoordinationFeeRate CoordinationFeeRate { get; init; }
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

	private int MaxVsizeInputOutputPair => AllowedOutputTypes.Max(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
	private ScriptType MaxVsizeInputOutputPairScriptType => AllowedOutputTypes.MaxBy(x => x.EstimateInputVsize() + x.EstimateOutputVsize());

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

	/// <returns>Min output amount that's economically reasonable to be registered with current network conditions.</returns>
	/// <remarks>It won't be smaller than min allowed output amount.</remarks>
	public Money CalculateMinReasonableOutputAmount()
	{
		var minEconomicalOutput = MiningFeeRate.GetFee(MaxVsizeInputOutputPair);
		return Math.Max(minEconomicalOutput, AllowedOutputAmounts.Min);
	}

	public Money CalculateSmallestReasonableEffectiveDenomination(WasabiRandom? random = null)
	{
		random ??= SecureRandom.Instance;
		return CalculateSmallestReasonableEffectiveDenomination(CalculateMinReasonableOutputAmount(), AllowedOutputAmounts.Max, MiningFeeRate, MaxVsizeInputOutputPairScriptType, random);
	}

	/// <returns>Smallest effective denom that's larger than min reasonable output amount. </returns>
	public static Money CalculateSmallestReasonableEffectiveDenomination(
		Money minReasonableOutputAmount,
		Money maxAllowedOutputAmount,
		FeeRate feeRate,
		ScriptType maxVsizeInputOutputPairScriptType,
		WasabiRandom random)
	{
		var smallestEffectiveDenom = DenominationBuilder.CreateDenominations(
				minReasonableOutputAmount,
				maxAllowedOutputAmount,
				feeRate,
				new List<ScriptType>() { maxVsizeInputOutputPairScriptType },
				random)
			.Min(x => x.EffectiveCost);

		return smallestEffectiveDenom is null
			? throw new InvalidOperationException("Something's wrong with the denomination creation or with the parameters it got.")
			: smallestEffectiveDenom;
	}

	/// <returns>Min: must be larger than the smallest economical denom. Max: max allowed in the round.</returns>
	public MoneyRange CalculateReasonableOutputAmountRange(WasabiRandom random) => new(CalculateSmallestReasonableEffectiveDenomination(random), AllowedOutputAmounts.Max);
}
