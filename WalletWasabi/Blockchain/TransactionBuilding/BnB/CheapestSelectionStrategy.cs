using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that searches search-space and caches every found selection that minimizes
/// waste of user's fund by looking for a selection that minimizes inputs' spending costs
/// and extra cost of paying more than specified target.
/// </summary>
public class CheapestSelectionStrategy : ISearchStrategy
{
	private long _currentInputCosts = 0;
	private long _bestTargetSoFar = long.MaxValue;
	private long[]? _bestSelectionSoFar;

	/// <param name="target">Value in satoshis.</param>
	/// <param name="inputSpendingCosts">Costs of spending coins in satoshis.</param>
	public CheapestSelectionStrategy(long target, long[] inputSpendingCosts)
	{
		Target = target;
		InputCosts = inputSpendingCosts;
	}

	/// <inheritdoc/>
	public long Target { get; }
	public long[] InputCosts { get; }

	/// <summary>Gives lowest found value selection whose sum is larger than or equal to <see cref="Target"/>.</summary>
	public long[]? GetBestSelectionFound() => _bestSelectionSoFar?.Where(x => x > 0).ToArray();

	/// <inheritdoc/>
	public long UpdateSum(NextAction action, long[] selection, int depth, long oldSum)
	{
		if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
		{
			_currentInputCosts += InputCosts[depth];
			return oldSum + selection[depth];
		}

		_currentInputCosts -= InputCosts[depth];
		return oldSum - selection[depth];
	}

	/// <inheritdoc/>
	public EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalSum = sum + _currentInputCosts;

		if (totalSum > _bestTargetSoFar)
		{
			// Our solution is already better than what we might get here.
			return EvaluationResult.SkipBranch;
		}
		else if (sum >= Target)
		{
			if (_bestTargetSoFar > totalSum)
			{
				_bestTargetSoFar = totalSum;
				_bestSelectionSoFar = selection[0..depth];
			}

			// Even if a match occurred we cannot be sure that there isn't
			// a better selection thanks to input costs.
			return EvaluationResult.SkipBranch;
		}
		else if (depth == selection.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}
}
