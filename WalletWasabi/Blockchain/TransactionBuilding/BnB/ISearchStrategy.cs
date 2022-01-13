namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Search strategy for Branch and Bound algorithm.
/// </summary>
public interface ISearchStrategy
{
	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	long Target { get; }

	/// <summary>
	/// Evaluation function that evaluates each step of the Branch and Bound algorithm and
	/// tells the algorithm what to do next.
	/// </summary>
	/// <param name="solution">Currently selected values. <c>0</c> when the corresponding value is not selected.</param>
	/// <param name="count">Number of <paramref name="solution"/> elements that contains the current solution.</param>
	/// <param name="sum">Sum of first <paramref name="count"/> elements of <paramref name="solution"/>.</param>
	EvaluationResult Evaluate(long[] solution, int count, long sum);
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