namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public class ExactMatchStrategy : BaseStrategy
{
	public ExactMatchStrategy(long target, long[] inputValues)
		: base(target, inputValues)
	{
	}

	/// <inheritdoc/>
	public override EvaluationResult Evaluate(long[] solution, int depth, long sum)
	{
		if (sum > Target)
		{
			// Excessive funds, cut the branch!
			return EvaluationResult.SkipBranch;
		}
		else if (sum == Target)
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
