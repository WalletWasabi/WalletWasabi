using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that searches search-space and caches every found selection that minimizes
/// waste of user's fund by looking for a selection that minimizes inputs' spending costs
/// and extra cost of paying more than specified target.
/// </summary>
public class CheapestSelectionStrategy
{
	private long _currentInputCosts = 0;
	private long _bestTargetSoFar;
	private long[]? _bestSelectionSoFar;
	private long _remainingAmount;

	/// <param name="target">Value in satoshis.</param>
	/// <param name="inputCosts">Costs of spending coins in satoshis.</param>
	public CheapestSelectionStrategy(SuggestionType suggestionType, long target, long[] inputValues, long[] inputCosts)
	{
		InputCosts = inputCosts;
		InputValues = inputValues;
		Target = target;
		SuggestionType = suggestionType;
		_bestTargetSoFar = suggestionType == SuggestionType.More ? long.MaxValue : long.MinValue;
		_remainingAmount = InputValues.Sum();
	}

	/// <summary>Costs corresponding to <see cref="InputValues"/> values.</summary>
	public long[] InputCosts { get; }

	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	public long Target { get; }

	/// <summary>Input values sorted in descending orders.</summary>
	public long[] InputValues { get; }

	/// <summary>Gives lowest found value selection whose sum is larger than or equal to <see cref="Target"/>.</summary>
	public long[]? GetBestSelectionFound() => _bestSelectionSoFar?.Where(x => x > 0).ToArray();

	// <summary>Suggestion type affecting whether selection sums to a larger or a smaller sum than the target.</summary>
	private SuggestionType SuggestionType { get; }

	/// <summary>
	/// Modifies selection sum so that we don't need to recompute it.
	/// </summary>
	/// <param name="action">Current action of the BnB algorithm.</param>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="depth">Index of a <paramref name="selection"/> value that is currently being included / omitted.</param>
	/// <param name="oldSum">Previous sum value.</param>
	/// <returns>New selection sum.</returns>
	public long ProcessAction(NextAction action, long[] selection, int depth, long oldSum)
	{
		long newSum;

		if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
		{
			if (selection[depth] == 0)
			{
				_currentInputCosts += InputCosts[depth];
				_remainingAmount -= InputValues[depth];
			}

			selection[depth] = InputValues[depth];
			newSum = oldSum + selection[depth];
		}
		else
		{
			if (selection[depth] > 0)
			{
				_currentInputCosts -= InputCosts[depth];
				_remainingAmount += InputValues[depth];
			}

			newSum = oldSum - selection[depth];
			selection[depth] = 0;
		}

		return newSum;
	}

	/// <summary>
	/// Evaluation function that evaluates each step of the Branch and Bound algorithm and
	/// tells the algorithm what to do next.
	/// </summary>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="depth">Number of <paramref name="selection"/> elements that contains the current solution.</param>
	/// <param name="sum">Sum of first <paramref name="depth"/> elements of <paramref name="selection"/>.</param>
	public EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + _currentInputCosts;

		if (SuggestionType is SuggestionType.More)
		{
			if (totalCost > _bestTargetSoFar)
			{
				// Our solution is already better than what we might get here.
				return EvaluationResult.SkipBranch;
			}
			else if (sum + _remainingAmount < Target)
			{
				// The remaining coins cannot sum up to required target, cut the branch.
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
		else if (SuggestionType is SuggestionType.Less)
		{
			if (totalCost > Target)
			{
				// Excessive funds, cut the branch.
				return EvaluationResult.SkipBranch;
			}

			if (sum + _remainingAmount < _bestTargetSoFar)
			{
				// The remaining coins cannot sum up to our solution, so cut the branch.
				return EvaluationResult.SkipBranch;
			}

			if (_bestTargetSoFar < totalCost)
			{
				_bestSelectionSoFar = selection[0..depth];
				_bestTargetSoFar = totalCost;
			}

			if (depth == selection.Length)
			{
				// Leaf reached, no match
				return EvaluationResult.SkipBranch;
			}

			return EvaluationResult.Continue;
		}
		else
		{
			throw new NotImplementedException($"{SuggestionType} is not supported.");
		}
	}
}
