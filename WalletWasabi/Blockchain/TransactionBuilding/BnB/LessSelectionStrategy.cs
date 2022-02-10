namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public class LessSelectionStrategy : SelectionStrategy
{
	public LessSelectionStrategy(long target, long[] inputValues, long[] inputCosts) : base(target, inputValues, inputCosts)
	{
		_bestTargetSoFar = long.MinValue;
	}

	public override EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + _currentInputCosts;

		if (totalCost > Target)
		{
			// Excessive funds, cut the branch.
			return EvaluationResult.SkipBranch;
		}

		if (sum + _remainingAmount < _bestTargetSoFar)
		{
			// The remaining coins cannot sum up to our solution, so cut the branch.
			return EvaluationResult.SkipBranch;
		}

		if (_bestTargetSoFar < totalCost)
		{
			_bestSelectionSoFar = selection[0..depth];
			_bestTargetSoFar = totalCost;
		}

		if (depth == selection.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}
}
