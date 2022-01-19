using System.Collections.Generic;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Search strategy for Branch and Bound algorithm.
/// </summary>
public interface ISearchStrategy
{
	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	long Target { get; }

	public List<long> InputValues { get; }

	/// <summary>
	/// Modifies selection sum so that we don't need to recompute it.
	/// </summary>
	/// <param name="action">Current action of the BnB algorithm.</param>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="depth">Index of a <paramref name="selection"/> value that is currently being included / omitted.</param>
	/// <param name="oldSum">Previous sum value.</param>
	/// <returns>New selection sum.</returns>
	long UpdateSum(NextAction action, long[] selection, int depth, long oldSum)
	{
		long result;

		if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
		{

			selection[depth] = InputValues[depth];
			result = oldSum + selection[depth];
		}
		else if (action == NextAction.OmitFirstThenInclude || action == NextAction.Omit)
		{
			result = oldSum - selection[depth];
			selection[depth] = 0;
		}
		else
		{
			result = oldSum - selection[depth];
			selection[depth] = 0;
		}

		return result;
	}

	/// <summary>
	/// Evaluation function that evaluates each step of the Branch and Bound algorithm and
	/// tells the algorithm what to do next.
	/// </summary>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="count">Number of <paramref name="selection"/> elements that contains the current solution.</param>
	/// <param name="sum">Sum of first <paramref name="count"/> elements of <paramref name="selection"/>.</param>
	EvaluationResult Evaluate(long[] selection, int count, long sum);
}

public enum EvaluationResult
{
	/// <summary>Select a value to the selection and continue on.</summary>
	Continue,

	/// <summary>Omit a value from the selection.</summary>
	SkipBranch,

	/// <summary>Match was found!</summary>
	Match
}