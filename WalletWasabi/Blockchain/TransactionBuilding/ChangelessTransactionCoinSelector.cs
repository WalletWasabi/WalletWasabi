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
	public static async IAsyncEnumerable<IReadOnlyList<SmartCoin>> GetAllStrategyResultsAsync(
		IReadOnlyList<SmartCoin> availableCoins,
		FeeRate feeRate,
		Money amount,
		Script referenceScriptPubKey,
		int maxInputCount,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		// target = target amount + output cost
		long target = amount + feeRate.GetFee(referenceScriptPubKey.EstimateOutputVsize()).Satoshi;

		// Group coins by their script pub key and sort the groups in the descending order. Each coin group is considered to be a single coin for the purposes of the algorithm.
		// All coins in a single group have the same script pub key so all the coins should be spent together or not spent at all.
		IOrderedEnumerable<IGrouping<Script, SmartCoin>> coinsByScript = availableCoins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.EffectiveValue(feeRate).Satoshi));

		// How much it costs to spend each coin group.
		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));

		// Pass smart coin groups effective values in descending order.
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
					if (TryGetCoins(strategy, inputEffectiveValues, out IReadOnlyList<SmartCoin>? coins, cancellationToken))
					{
						return coins;
					}

					return null;
				},
				cancellationToken))
			.ToArray();

		foreach (var task in tasks)
		{
			IReadOnlyList<SmartCoin>? smartCoins = await task.ConfigureAwait(false);

			if (smartCoins is not null)
			{
				yield return smartCoins;
			}
		}
	}

	/// <summary>
	/// Select coins in a way that user can pay without a change output (to increase privacy)
	/// and try to find a solution that requires to pay as little extra amount as possible.
	/// </summary>
	/// <param name="strategy">The strategy determines what the algorithm is looking for.</param>
	/// <param name="inputEffectiveValues">Dictionary to map back the effective values to their original SmartCoin. </param>
	/// <param name="selectedCoins">Out parameter that returns (non-grouped!) coins back.</param>
	/// <returns><c>true</c> if a solution was found, <c>false</c> otherwise.</returns>
	internal static bool TryGetCoins(SelectionStrategy strategy, Dictionary<SmartCoin[], long> inputEffectiveValues, [NotNullWhen(true)] out IReadOnlyList<SmartCoin>? selectedCoins, CancellationToken cancellationToken)
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

			foreach ((SmartCoin[] smartCoinGroup, long effectiveSatoshis) in inputEffectiveValues)
			{
				// Both arrays are in decreasing order so the first match will be the coin we are looking for.
				if (effectiveSatoshis == solution[i])
				{
					i++;
					resultCoins.AddRange(smartCoinGroup);
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
