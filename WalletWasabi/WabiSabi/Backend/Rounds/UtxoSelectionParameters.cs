using NBitcoin;
using System.Collections.Immutable;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

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
			roundParameters.CalculateReasonableOutputAmountRange(random),
			roundParameters.CoordinationFeeRate,
			roundParameters.MiningFeeRate,
			roundParameters.AllowedInputTypes);
	}
}
