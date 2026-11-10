using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public record UtxoSelectionParameters(
	MoneyRange AllowedInputAmounts,
	Money MinAllowedOutputAmount,
	FeeRate MiningFeeRate,
	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes)
{
	public bool Allows(SmartCoin coin) =>
		AllowedInputAmounts.Contains(coin.Amount)
		&& AllowedInputScriptTypes.Contains(coin.ScriptType)
		&& coin.EffectiveValue(MiningFeeRate) > Money.Zero;

	public static UtxoSelectionParameters FromRoundParameters(RoundParameters roundParameters, ImmutableHashSet<ScriptType> scriptTypesSupportedByWallet)
	{
		var outputTypes = roundParameters.AllowedOutputTypes.Intersect(scriptTypesSupportedByWallet);
		var maxVsizeInputOutputPairScriptType = outputTypes.MaxBy(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
		var smallestDenomination = DenominationBuilder.CreateDenominations(
				roundParameters.CalculateMinReasonableOutputAmount(scriptTypesSupportedByWallet),
				roundParameters.AllowedOutputAmounts.Max)
			.Min()
			?? throw new InvalidOperationException("Something's wrong with the denomination creation or with the parameters it got.");
		var smallestReasonableEffectiveDenomination = Output.FromDenomination(smallestDenomination, maxVsizeInputOutputPairScriptType, roundParameters.MiningFeeRate).EffectiveCost;

		return new(
			roundParameters.AllowedInputAmounts,
			smallestReasonableEffectiveDenomination,
			roundParameters.MiningFeeRate,
			roundParameters.AllowedInputTypes);
	}
}

public static class RoundParametersExtensions
{
	/// <returns>Min: must be larger than the smallest economical denom. Max: max allowed in the round.</returns>
	/// <returns>Min output amount that's economically reasonable to be registered with current network conditions.</returns>
	/// <remarks>It won't be smaller than min allowed output amount.</remarks>
	public static Money CalculateMinReasonableOutputAmount(this RoundParameters roundParameters, ImmutableHashSet<ScriptType> scriptTypesSupportedByWallet)
	{
		var outputTypes = roundParameters.AllowedOutputTypes.Intersect(scriptTypesSupportedByWallet);
		var maxVsizeInputOutputPair = outputTypes.Max(x => x.EstimateInputVsize() + x.EstimateOutputVsize());
		var minEconomicalOutput = roundParameters.MiningFeeRate.GetFee(maxVsizeInputOutputPair);
		return Math.Max(minEconomicalOutput, roundParameters.AllowedOutputAmounts.Min);
	}
}
