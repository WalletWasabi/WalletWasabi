using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that searches search-space and caches every found selection that minimizes
/// waste of user's fund by looking for a selection that minimizes inputs' spending costs
/// and extra cost of paying more than specified target.
/// </summary>
public abstract class SelectionStrategy
{
	public SelectionStrategy(long target, long[] inputValues, long[] inputCosts)
	{
		InputCosts = inputCosts;
		InputValues = inputValues;
		Target = target;
		RemainingAmount = InputValues.Sum();
	}

	/// <summary>Costs corresponding to <see cref="InputValues"/> values.</summary>
	public long[] InputCosts { get; }

	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	public long Target { get; }

	/// <summary>Input values sorted in descending orders.</summary>
	public long[] InputValues { get; }

	/// <summary>Gives lowest found value selection whose sum is larger than or equal to <see cref="Target"/>.</summary>
	public long[]? GetBestSelectionFound() => BestSelectionSoFar?.Where(x => x > 0).ToArray();

	/// <summary>Input cost(s) of the current selection.</summary>
	protected long CurrentInputCosts { get; set; } = 0;

	/// <summary>Sum of the selectable/remaining coins.</summary>
	protected long RemainingAmount { get; set; }

	/// <summary>Sum of the best found selection.</summary>
	protected long BestTargetSoFar { get; set; }

	/// <summary>Best coin selection so far.</summary>
	protected long[]? BestSelectionSoFar { get; set; }

	/// <summary>
	/// Modifies selection sum so that we don't need to recompute it.
	/// </summary>
	/// <param name="action">Current action of the BnB algorithm.</param>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="depth">Index of a <paramref name="selection"/> value that is currently being included / omitted.</param>
	/// <param name="oldSum">Previous sum value.</param>
	/// <returns>New selection sum.</returns>
	public virtual long ProcessAction(NextAction action, long[] selection, int depth, long oldSum)
	{
		long newSum;

		if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
		{
			if (selection[depth] == 0)
			{
				CurrentInputCosts += InputCosts[depth];
				RemainingAmount -= InputValues[depth];
			}

			selection[depth] = InputValues[depth];
			newSum = oldSum + selection[depth];
		}
		else
		{
			if (selection[depth] > 0)
			{
				CurrentInputCosts -= InputCosts[depth];
				RemainingAmount += InputValues[depth];
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
	public abstract EvaluationResult Evaluate(long[] selection, int depth, long sum);
}
