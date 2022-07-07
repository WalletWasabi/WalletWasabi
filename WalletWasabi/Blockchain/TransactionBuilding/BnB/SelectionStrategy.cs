using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public abstract class SelectionStrategy
{
	/// <param name="parameters">Parameters of the strategy specifying input coins, the target and final selection restrictions.</param>
	/// <param name="bestSelection">Best selection so far.</param>
	public SelectionStrategy(StrategyParameters parameters, CoinSelection bestSelection)
	{
		Parameters = parameters;
		BestSelection = bestSelection;

		RemainingAmounts = new long[InputValues.Length];
		long accumulator = InputValues.Sum();

		for (int i = 0; i < InputValues.Length; i++)
		{
			accumulator -= InputValues[i];
			RemainingAmounts[i] = accumulator;
		}
	}

	public StrategyParameters Parameters { get; }

	/// <inheritdoc cref="StrategyParameters.InputCosts"/>
	public long[] InputCosts => Parameters.InputCosts;

	/// <inheritdoc cref="StrategyParameters.Target"/>
	public long Target => Parameters.Target;

	/// <inheritdoc cref="StrategyParameters.InputValues"/>
	public long[] InputValues => Parameters.InputValues;

	/// <summary>Number of coins included in current selection.</summary>
	/// <remarks>Range of values is <c>0</c> to <see cref="InputValues"/> size.</remarks>
	protected int IncludedCoinsCount { get; set; } = 0;

	/// <summary>Holds best coin selection found so far with some metadata to improve performance.</summary>
	protected CoinSelection BestSelection { get; }

	/// <summary>Input cost(s) of the current selection.</summary>
	protected long CurrentInputCosts { get; set; } = 0;

	/// <summary>Sums of the remaining coins.</summary>
	/// <remarks>Each i-th element represents a sum of all <c>i+1, i+2, ..., n</c> input values.</remarks>
	protected long[] RemainingAmounts { get; set; }

	/// <summary>Gets best valid found selection as an array of effective values, or <c>null</c> if none was found.</summary>
	public virtual long[]? GetBestSelectionFound() => BestSelection.GetSolutionArray();

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

			IncludedCoinsCount++;
			selection[depth] = InputValues[depth];
			newSum = oldSum + selection[depth];
		}
		else
		{
			if (selection[depth] > 0)
			{
				IncludedCoinsCount--;
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
