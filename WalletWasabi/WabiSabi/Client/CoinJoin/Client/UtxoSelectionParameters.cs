using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public record UtxoSelectionParameters(
	MoneyRange AllowedInputAmounts,
	Money MinAllowedOutputAmount,
	FeeRate MiningFeeRate,
	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes)
{
	public bool Allows(SmartCoin coin) => GetIneligibility(coin) is null;

	/// <returns>Why the round does not accept the coin, or <c>null</c> if it accepts it.</returns>
	/// <remarks>The reasons are checked in the order the coordinator checks them when an input is registered.</remarks>
	public CoinJoinIneligibility? GetIneligibility(SmartCoin coin) =>
		coin switch
		{
			_ when !AllowedInputScriptTypes.Contains(coin.ScriptType) => new CoinJoinIneligibility.ScriptTypeNotAllowed(),
			_ when coin.Amount < AllowedInputAmounts.Min => new CoinJoinIneligibility.AmountTooLow(AllowedInputAmounts.Min),
			_ when coin.Amount > AllowedInputAmounts.Max => new CoinJoinIneligibility.AmountTooHigh(AllowedInputAmounts.Max),
			_ when coin.EffectiveValue(MiningFeeRate) <= Money.Zero => new CoinJoinIneligibility.Uneconomical(MiningFeeRate),
			_ => null
		};

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
