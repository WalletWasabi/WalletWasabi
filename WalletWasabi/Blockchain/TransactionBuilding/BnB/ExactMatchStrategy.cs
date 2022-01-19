using System.Collections.Generic;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public class ExactMatchStrategy : ISearchStrategy
{
	/// <inheritdoc/>
	public long Target { get; }

	public List<long> InputValues { get; }

	public ExactMatchStrategy(long target, List<long> inputValues)
	{
		Target = target;
		InputValues = inputValues;
	}

	/// <inheritdoc/>
	public EvaluationResult Evaluate(long[] solution, int depth, long effValue)
	{
		if (effValue > Target)
		{
			// Excessive funds, cut the branch!
			return EvaluationResult.SkipBranch;
		}
		else if (effValue == Target)
		{
			// Match found!
			return EvaluationResult.Match;
		}
		else if (depth == solution.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}
}
