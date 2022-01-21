using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class ChangelessTransactionCoinSelector
{
	/// <summary>Payments are capped to be at most 25% more expensive than the original target.</summary>
	public const double MaxExtraFee = 1.25;

	/// <summary>
	/// Select coins in a way that user can pay without a change input (to increase privacy)
	/// and try to find a solution that requires to pay as little extra amount as possible.
	/// </summary>
	/// <param name="availableCoins">Coins owned by the user.</param>
	/// <param name="feeRate">Current fee rate to take into account effective values of available coins.</param>
	/// <param name="targetAmount">Amount the user wants to pay.</param>
	/// <returns><c>true</c> if a solution was found, <c>false</c> otherwise.</returns>
	/// <remarks>The implementation gives only the guarantee that user can pay at most 25% more than <paramref name="targetAmount"/>.</remarks>
	public static bool TryGetCoins(IEnumerable<SmartCoin> availableCoins, FeeRate feeRate, Money targetAmount, [NotNullWhen(true)] out IEnumerable<SmartCoin>? selectedCoins, CancellationToken cancellationToken = default)
	{
		selectedCoins = null;
		var target = targetAmount.Satoshi;

		// Keys are effective values of smart coins in satoshis.
		IOrderedEnumerable<SmartCoin> sortedCoins = availableCoins.OrderByDescending(x => x.EffectiveValue(feeRate).Satoshi);

		// How much it costs to spend each coin.
		long[] inputCosts = sortedCoins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin, long> inputEffectiveValues = new(sortedCoins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));

		// Pass smart coins' effective values in descending order.
		long[] inputValues = inputEffectiveValues.Values.ToArray();

		BranchAndBound branchAndBound = new();
		CheapestSelectionStrategy strategy = new(target, inputValues, inputCosts);

		var foundExactMatch = branchAndBound.TryGetMatch(strategy, out List<long>? solution, cancellationToken);

		// If we've not found and optimal solution than we will use the best.
		if (!foundExactMatch && strategy.GetBestSelectionFound() is long[] bestSolution)
		{
			solution = bestSolution.ToList();
		}

		if (solution is not null)
		{
			// Sanity check: do not return solution that is too expensive.
			if (solution.Sum() > target * MaxExtraFee)
			{
				return false;
			}

			List<SmartCoin> resultCoins = new();
			int i = 0;

			foreach ((SmartCoin smartCoin, long effectiveSatoshis) in inputEffectiveValues)
			{
				// Both array are in decreasing order so the first match will be the coin we are looking for.
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
