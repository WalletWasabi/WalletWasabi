using System.Collections.Generic;
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
	Money MinAllowedOutputAmount,
	CoordinationFeeRate CoordinationFeeRate,
	FeeRate MiningFeeRate,
	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes)
{
	public static UtxoSelectionParameters FromRoundParameters(RoundParameters roundParameters, ScriptType[] scriptTypesSupportedByWallet)
	{
		var outputTypes = roundParameters.AllowedOutputTypes.Intersect(scriptTypesSupportedByWallet);
		var maxVsizeInputOutputPairScriptType = outputTypes.MaxBy(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
		var smallestEffectiveDenom = DenominationBuilder.CreateDenominations(
				roundParameters.CalculateMinReasonableOutputAmount(scriptTypesSupportedByWallet),
				roundParameters.AllowedOutputAmounts.Max,
				roundParameters.MiningFeeRate,
				[maxVsizeInputOutputPairScriptType],
				new InsecureRandom()) // Random generator is not used and then the algorithm is deterministic
			.Min(x => x.EffectiveCost);
		var smallestReasonableEffectiveDenomination =
			smallestEffectiveDenom
		    ?? throw new InvalidOperationException("Something's wrong with the denomination creation or with the parameters it got.");

		return new(
			roundParameters.AllowedInputAmounts,
			smallestReasonableEffectiveDenomination,
			roundParameters.CoordinationFeeRate,
			roundParameters.MiningFeeRate,
			roundParameters.AllowedInputTypes);
	}
}

public static class RoundParametersExtensions
{
	/// <returns>Min: must be larger than the smallest economical denom. Max: max allowed in the round.</returns>
	/// <returns>Min output amount that's economically reasonable to be registered with current network conditions.</returns>
	/// <remarks>It won't be smaller than min allowed output amount.</remarks>
	public static Money CalculateMinReasonableOutputAmount(this RoundParameters roundParameters, IEnumerable<ScriptType> scriptTypesSupportedByWallet)
	{
		var outputTypes = roundParameters.AllowedOutputTypes.Intersect(scriptTypesSupportedByWallet);
		var maxVsizeInputOutputPair = outputTypes.Max(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
		var minEconomicalOutput = roundParameters.MiningFeeRate.GetFee(maxVsizeInputOutputPair);
		return Math.Max(minEconomicalOutput, roundParameters.AllowedOutputAmounts.Min);
	}
}
