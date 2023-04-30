using System.Collections.Generic;
using System.Collections.Immutable;
using NBitcoin;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabiClientLibrary.Models.SelectInputsForRound;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

/// <summary>
/// Represents a set of available UTXOs and various parameters to select a set of UTXOs satisfying all conditions for a CoinJoin round.
/// </summary>
/// <param name="Utxos">Set of the UTXOs to choose from.</param>
/// <param name="AnonScoreTarget">Required anonymity score target.</param>
/// <param name="ConsolidationMode">This option will likely be removed. We expect value to be <c>false</c>.</param>
public record SelectInputsForRoundRequest(
	MoneyRange AllowedInputAmounts,
	MoneyRange AllowedOutputAmounts,
	CoordinationFeeRate CoordinationFeeRate,
	FeeRate MiningFeeRate,
	IEnumerable<ScriptType> AllowedInputTypes,
	Utxo[] Utxos,
	int AnonScoreTarget,
	int SemiPrivateThreshold,
	Money LiquidityClue,
	bool ConsolidationMode = false,
	bool DoNotSelectPrivateCoins = false
);
