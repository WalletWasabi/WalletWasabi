using NBitcoin;
using System.Collections.Immutable;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public record UtxoSelectionParameters(
	MoneyRange AllowedInputAmounts,
	MoneyRange AllowedOutputAmounts,
	CoordinationFeeRate CoordinationFeeRate,
	FeeRate MiningFeeRate,
	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes)
{
	public static UtxoSelectionParameters FromRoundParameters(RoundParameters roundParameters, WasabiRandom? random = null)
	{
		random ??= SecureRandom.Instance;
		return new(
			roundParameters.AllowedInputAmounts,
			ReasonableOutputAmountRange(roundParameters, random),
			roundParameters.CoordinationFeeRate,
			roundParameters.MiningFeeRate,
			roundParameters.AllowedInputTypes);
	}

	private static MoneyRange ReasonableOutputAmountRange(RoundParameters roundParameters, WasabiRandom random)
	{
		var scriptTypesSupportedByWallet= new[] { ScriptType.P2WPKH, ScriptType.Taproot }; // I doubt this will change
		var outputTypes = roundParameters.AllowedOutputTypes.Intersect(scriptTypesSupportedByWallet);
		var maxVsizeInputOutputPairScriptType = outputTypes.MaxBy(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
		var smallestEffectiveDenom = DenominationBuilder.CreateDenominations(
				roundParameters.CalculateMinReasonableOutputAmount(),
				roundParameters.AllowedOutputAmounts.Max,
				roundParameters.MiningFeeRate,
				[maxVsizeInputOutputPairScriptType],
				random)
			.Min(x => x.EffectiveCost);
		var smallestReasonableEffectiveDenomination =
			smallestEffectiveDenom
		    ?? throw new InvalidOperationException("Something's wrong with the denomination creation or with the parameters it got.");

		return roundParameters.AllowedOutputAmounts with { Min = smallestReasonableEffectiveDenomination };
	}
}

public static class RoundParametersExtensions
{
	/// <returns>Min: must be larger than the smallest economical denom. Max: max allowed in the round.</returns>
	/// <returns>Min output amount that's economically reasonable to be registered with current network conditions.</returns>
	/// <remarks>It won't be smaller than min allowed output amount.</remarks>
	public static Money CalculateMinReasonableOutputAmount(this RoundParameters roundParameters)
	{
		var maxVsizeInputOutputPair = roundParameters.AllowedOutputTypes.Max(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
		var minEconomicalOutput = roundParameters.MiningFeeRate.GetFee(maxVsizeInputOutputPair);
		return Math.Max(minEconomicalOutput, roundParameters.AllowedOutputAmounts.Min);
	}
}
