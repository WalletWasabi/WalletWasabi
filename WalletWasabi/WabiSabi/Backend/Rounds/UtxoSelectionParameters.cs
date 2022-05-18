using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public interface IUtxoSelectionParameters
{
	MoneyRange AllowedInputAmounts { get; }
	MoneyRange AllowedOutputAmounts { get; }
	CoordinationFeeRate CoordinationFeeRate { get; }
	FeeRate MiningFeeRate { get; }

	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes { get; }
}
