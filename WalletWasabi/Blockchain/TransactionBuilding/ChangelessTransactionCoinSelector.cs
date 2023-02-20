using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class ChangelessTransactionCoinSelector
{
	public static async IAsyncEnumerable<IEnumerable<SmartCoin>> GetAllStrategyResultsAsync(
		IEnumerable<SmartCoin> availableCoins,
		FeeRate feeRate,
		TxOut txOut,
		int maxInputCount,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		// target = target amount + output cost
		long target = txOut.Value.Satoshi + feeRate.GetFee(txOut.ScriptPubKey.EstimateOutputVsize()).Satoshi;

		// Keys are effective values of smart coins in satoshis.
		IOrderedEnumerable<SmartCoin> sortedCoins = availableCoins.OrderByDescending(x => x.EffectiveValue(feeRate).Satoshi);

		// How much it costs to spend each coin.
		long[] inputCosts = sortedCoins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin, long> inputEffectiveValues = new(sortedCoins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));

		// Pass smart coins' effective values in descending order.
		long[] inputValues = inputEffectiveValues.Values.ToArray();

		StrategyParameters parameters = new(target, inputValues, inputCosts, maxInputCount);

		SelectionStrategy[] strategies = new SelectionStrategy[]
		{
			new MoreSelectionStrategy(parameters),
			new LessSelectionStrategy(parameters)
		};

		var tasks = strategies
			.Select(strategy => Task.Run(
				() =>
				{
					if (TryGetCoins(strategy, inputEffectiveValues, out IEnumerable<SmartCoin>? coins, cancellationToken))
					{
						return coins;
					}

					return Enumerable.Empty<SmartCoin>();
				},
				cancellationToken))
			.ToArray();

		foreach (var task in tasks)
		{
			yield return await task.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Select coins in a way that user can pay without a change output (to increase privacy)
	/// and try to find a solution that requires to pay as little extra amount as possible.
	/// </summary>
	/// <param name="strategy">The strategy determines what the algorithm is looking for.</param>
	/// <param name="inputEffectiveValues">Dictionary to map back the effective values to their original SmartCoin. </param>
	/// <returns><c>true</c> if a solution was found, <c>false</c> otherwise.</returns>
	internal static bool TryGetCoins(SelectionStrategy strategy, Dictionary<SmartCoin, long> inputEffectiveValues, [NotNullWhen(true)] out IEnumerable<SmartCoin>? selectedCoins, CancellationToken cancellationToken)
	{
		selectedCoins = null;

		BranchAndBound branchAndBound = new();

		bool foundExactMatch = false;
		List<long>? solution = null;

		try
		{
			foundExactMatch = branchAndBound.TryGetMatch(strategy, out solution, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Logger.LogInfo("Computing privacy suggestions was cancelled or timed out.");
		}

		// If we've not found an optimal solution then we will use the best.
		if (!foundExactMatch && strategy.GetBestSelectionFound() is long[] bestSolution)
		{
			solution = bestSolution.ToList();
		}

		if (solution is not null)
		{
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
