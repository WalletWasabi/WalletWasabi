using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that searches search-space and caches every found selection that minimizes
/// waste of user's fund by looking for a selection that minimizes inputs' spending costs
/// and extra cost of paying more than specified target.
/// </summary>
public class CheapestSelectionStrategy : BaseStrategy
{
	private long _currentInputCosts = 0;
	private long _bestTargetSoFar = long.MaxValue;
	private long[]? _bestSelectionSoFar;

	/// <param name="target">Value in satoshis.</param>
	/// <param name="inputSpendingCosts">Costs of spending coins in satoshis.</param>
	public CheapestSelectionStrategy(long target, long[] inputValues, long[] inputSpendingCosts)
		: base(target, inputValues)
	{
		InputCosts = inputSpendingCosts;
	}

	/// <summary>Costs corresponding to <see cref="ISearchStrategy.InputValues"/> values.</summary>
	public long[] InputCosts { get; }

	/// <summary>Gives lowest found value selection whose sum is larger than or equal to <see cref="ISearchStrategy.Target"/>.</summary>
	public long[]? GetBestSelectionFound() => _bestSelectionSoFar?.Where(x => x > 0).ToArray();

	/// <inheritdoc/>
	public override long ProcessAction(NextAction action, long[] selection, int depth, long oldSum)
	{
		long newSum;

		if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
		{
			if (selection[depth] == 0)
			{
				_currentInputCosts += InputCosts[depth];
			}

			selection[depth] = InputValues[depth];
			newSum = oldSum + selection[depth];
		}
		else if (action == NextAction.OmitFirstThenInclude || action == NextAction.Omit)
		{
			if (selection[depth] > 0)
			{
				_currentInputCosts -= InputCosts[depth];
			}

			newSum = oldSum - selection[depth];
			selection[depth] = 0;
		}
		else
		{
			if (selection[depth] > 0)
			{
				_currentInputCosts -= InputCosts[depth];
			}

			newSum = oldSum - selection[depth];
			selection[depth] = 0;
		}

		return newSum;
	}

	/// <inheritdoc/>
	public override EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + _currentInputCosts;

		if (totalCost > _bestTargetSoFar)
		{
			// Our solution is already better than what we might get here.
			return EvaluationResult.SkipBranch;
		}
		else if (sum >= Target)
		{
			if (_bestTargetSoFar > totalCost)
			{
				_bestSelectionSoFar = selection[0..depth];
				_bestTargetSoFar = totalCost;
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
