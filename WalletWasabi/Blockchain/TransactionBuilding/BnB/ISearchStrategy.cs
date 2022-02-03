namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Search strategy for Branch and Bound algorithm.
/// </summary>
public interface ISearchStrategy
{
	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	long Target { get; }

	long[] InputValues { get; }

	long[] InputCosts { get; }

	long[]? GetBestSelectionFound();

	/// <summary>
	/// Modifies selection sum so that we don't need to recompute it.
	/// </summary>
	/// <param name="action">Current action of the BnB algorithm.</param>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="depth">Index of a <paramref name="selection"/> value that is currently being included / omitted.</param>
	/// <param name="oldSum">Previous sum value.</param>
	/// <returns>New selection sum.</returns>
	long ProcessAction(NextAction action, long[] selection, int depth, long oldSum);

	/// <summary>
	/// Evaluation function that evaluates each step of the Branch and Bound algorithm and
	/// tells the algorithm what to do next.
	/// </summary>
	/// <param name="selection">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="depth">Number of <paramref name="selection"/> elements that contains the current solution.</param>
	/// <param name="sum">Sum of first <paramref name="depth"/> elements of <paramref name="selection"/>.</param>
	EvaluationResult Evaluate(long[] selection, int depth, long sum);
}
