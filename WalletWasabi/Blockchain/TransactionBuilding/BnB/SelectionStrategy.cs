using System.Diagnostics;
using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public abstract class SelectionStrategy
{
	/// <param name="target">Value in satoshis.</param>
	/// <param name="inputValues">Values in satoshis of the coins the user has (in descending order).</param>
	/// <param name="inputCosts">Costs of spending coins in satoshis.</param>
	/// <param name="bestSelection">Best selection so far.</param>
	public SelectionStrategy(long target, long[] inputValues, long[] inputCosts, CoinSelection bestSelection)
	{
		InputCosts = inputCosts;
		InputValues = inputValues;
		Target = target;
		BestSelection = bestSelection;

		RemainingAmounts = new long[inputValues.Length];
		long accumulator = InputValues.Sum();

		for (int i = 0; i < inputValues.Length; i++)
		{
			accumulator -= inputValues[i];
			RemainingAmounts[i] = accumulator;
		}
	}

	/// <summary>Costs corresponding to <see cref="InputValues"/> values.</summary>
	public long[] InputCosts { get; }

	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	public long Target { get; }

	/// <summary>Input values sorted in descending orders.</summary>
	public long[] InputValues { get; }

	/// <summary>Holds best coin selection found so far with some metadata to improve performance.</summary>
	protected CoinSelection BestSelection { get; }

	/// <summary>Gets best found selection as an array of effective values, or <c>null</c> if none was found.</summary>
	public long[]? GetBestSelectionFound() => BestSelection.GetSolutionArray();

	/// <summary>Input cost(s) of the current selection.</summary>
	protected long CurrentInputCosts { get; set; } = 0;

	/// <summary>Sums of the remaining coins.</summary>
	/// <remarks>i-th element represents a sum of all <c>i+1, i+2, ..., n</c> input values.</remarks>
	protected long[] RemainingAmounts { get; set; }

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
			}

			selection[depth] = InputValues[depth];
			newSum = oldSum + selection[depth];
		}
		else
		{
			if (selection[depth] > 0)
			{
				CurrentInputCosts -= InputCosts[depth];
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
