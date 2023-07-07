using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabiClientLibrary.Models;
using WalletWasabi.WabiSabiClientLibrary.Models.SelectInputsForRound;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;

public class SelectInputsForRoundHelper
{
	public static SelectInputsForRoundResponse SelectInputsForRound(SelectInputsForRoundRequest request, WasabiRandom random)
	{
		MoneyRange reasonableOutputAmountRange = new(RoundParameters.CalculateSmallestReasonableEffectiveDenomination(request.AllowedOutputAmounts.Min, request.AllowedOutputAmounts.Max, request.MiningFeeRate, ScriptType.Taproot, random), request.AllowedOutputAmounts.Max);
		UtxoSelectionParameters utxoSelectionParameters = new(request.AllowedInputAmounts, reasonableOutputAmountRange, request.CoordinationFeeRate, request.MiningFeeRate, request.AllowedInputTypes.ToImmutableSortedSet());
		CoinJoinCoinSelectorRandomnessGenerator randomnessGenerator = new(CoinJoinCoinSelector.MaxInputsRegistrableByWallet, random);
		IEnumerable<Utxo> utxos = request.DoNotSelectPrivateCoins ? request.Utxos.Where(x => x.AnonymitySet < request.AnonScoreTarget) : request.Utxos;
		CoinJoinCoinSelector coinJoinCoinSelector = new(request.ConsolidationMode, request.AnonScoreTarget, request.SemiPrivateThreshold, randomnessGenerator);
		ImmutableList<Utxo> coins = coinJoinCoinSelector.SelectCoinsForRound<Utxo>(utxos, utxoSelectionParameters, request.LiquidityClue);

		Dictionary<ISmartCoin, int> coinIndices = request.Utxos
			.Select((x, i) => ((ISmartCoin)x, i))
			.ToDictionary(x => x.Item1, x => x.i);

		// Find corresponding indices for the found coins.
		int[] indices = coins.Select(c => coinIndices[c]).Order().ToArray();

		return new SelectInputsForRoundResponse(indices);
	}
}
