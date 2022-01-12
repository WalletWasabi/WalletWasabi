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
	EvaluationResult Evaluate(long[] solution, int depth, long effValue);
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