using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public record UtxoSelectionParameters(
	MoneyRange AllowedInputAmounts,
	MoneyRange AllowedOutputAmounts,
	CoordinationFeeRate CoordinationFeeRate,
	FeeRate MiningFeeRate,
	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes)
{
	public static UtxoSelectionParameters FromRoundParameters(RoundParameters roundParameters) =>
		new(
			roundParameters.AllowedInputAmounts,
			roundParameters.CalculateReasonableOutputAmountRange(),
			roundParameters.CoordinationFeeRate,
			roundParameters.MiningFeeRate,
			roundParameters.AllowedInputTypes);
}
