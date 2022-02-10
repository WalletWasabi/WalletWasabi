using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class ChangelessTransactionCoinSelector
{
	/// <summary>Payments are capped to be at most 25% higher than the original target.</summary>
	public const double MaxExtraPayment = 1.25;

	/// <summary>Payments are capped to be at most 25% lower than the original target.</summary>
	public const double MinPaymentThreshold = 0.75;

	/// <summary>
	/// Select coins in a way that user can pay without a change output (to increase privacy)
	/// and try to find a solution that requires to pay as little extra amount as possible.
	/// </summary>
	/// <param name="availableCoins">Coins owned by the user.</param>
	/// <param name="feeRate">Current fee rate to take into account effective values of available coins.</param>
	/// <param name="txOut">Amount the user wants to pay + the type of the output address</param>
	/// <returns><c>true</c> if a solution was found, <c>false</c> otherwise.</returns>
	/// <remarks>The implementation gives only the guarantee that user can pay at most 25% more than <paramref name="txOut.Value"/>.</remarks>
	public static bool TryGetCoins(SuggestionType suggestionType, IEnumerable<SmartCoin> availableCoins, FeeRate feeRate, TxOut txOut, [NotNullWhen(true)] out IEnumerable<SmartCoin>? selectedCoins, CancellationToken cancellationToken = default)
	{
		selectedCoins = null;

		// target = target amount + output cost
		var target = txOut.Value.Satoshi + feeRate.GetFee(txOut.ScriptPubKey.EstimateOutputVsize()).Satoshi;

		// Keys are effective values of smart coins in satoshis.
		IOrderedEnumerable<SmartCoin> sortedCoins = availableCoins.OrderByDescending(x => x.EffectiveValue(feeRate).Satoshi);

		// How much it costs to spend each coin.
		long[] inputCosts = sortedCoins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin, long> inputEffectiveValues = new(sortedCoins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));

		// Pass smart coins' effective values in descending order.
		long[] inputValues = inputEffectiveValues.Values.ToArray();

		BranchAndBound branchAndBound = new();
		SelectionStrategy strategy = suggestionType == SuggestionType.More ? new MoreSelectionStrategy(target, inputValues, inputCosts) : new LessSelectionStrategy(target, inputValues, inputCosts);

		var foundExactMatch = branchAndBound.TryGetMatch(strategy, out List<long>? solution, cancellationToken);

		// If we've not found an optimal solution then we will use the best.
		if (!foundExactMatch && strategy.GetBestSelectionFound() is long[] bestSolution)
		{
			solution = bestSolution.ToList();
		}

		if (solution is not null)
		{
			// Sanity check: do not return solution that is much higher or much lower than the target.
			if (solution.Sum() > target * MaxExtraPayment || solution.Sum() < target * MinPaymentThreshold)
			{
				return false;
			}

			List<SmartCoin> resultCoins = new();
			int i = 0;

			foreach ((SmartCoin smartCoin, long effectiveSatoshis) in inputEffectiveValues)
			{
				// Both arrays are in decreasing order so the first match will be the coin we are looking for.
				if (effectiveSatoshis == solution[i])
				{
					i++;
					resultCoins.Add(smartCoin);
					if (i == solution.Count)
					{
						break;
					}
				}
			}

			selectedCoins = resultCoins;
			return true;
		}

		return false;
	}
}
