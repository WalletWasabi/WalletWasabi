namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public class LessSelectionStrategy : SelectionStrategy
{
	public LessSelectionStrategy(long target, long[] inputValues, long[] inputCosts) : base(target, inputValues, inputCosts)
	{
		BestTargetSoFar = long.MinValue;
	}

	public override EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + CurrentInputCosts;

		if (totalCost > Target)
		{
			// Excessive funds, cut the branch.
			return EvaluationResult.SkipBranch;
		}

		if (BestTargetSoFar < totalCost)
		{
			BestSelectionSoFar = selection[0..depth];
			BestTargetSoFar = totalCost;
		}

		if (depth == selection.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}
}
