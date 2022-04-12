namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that searches search-space and caches every found selection that minimizes
/// waste of user's fund by looking for a selection that minimizes inputs' spending costs
/// and extra cost of paying more than specified target.
/// </summary>
public class MoreSelectionStrategy : SelectionStrategy
{
	/// <summary>Payments are capped to be at most 25% higher than the original target.</summary>
	public const double MaxExtraPayment = 1.25;

	/// <inheritdoc/>
	public MoreSelectionStrategy(long target, long[] inputValues, long[] inputCosts)
		: base(target, inputValues, inputCosts, new CoinSelection(long.MaxValue, long.MaxValue))
	{
		MaximumTarget = (long)(target * MaxExtraPayment);
	}

	/// <summary>Maximum acceptable target (inclusive).</summary>
	/// <seealso cref="SelectionStrategy.Target"/>
	public long MaximumTarget { get; }

	public override EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + CurrentInputCosts;

		if (sum > BestSelection.PaymentAmount || sum > MaximumTarget)
		{
			// Our solution is already better than what we might get here.
			return EvaluationResult.SkipBranch;
		}

		if (sum >= Target)
		{
			if (CompareFitness(IncludedCoinsCount, sum, totalCost))
			{
				BestSelection.Update(sum, totalCost, IncludedCoinsCount, selection[0..depth]);
			}

			// Even if a match occurred we cannot be sure that there isn't
			// a better selection thanks to input costs.
			return EvaluationResult.SkipBranch;
		}

		if (sum + RemainingAmounts[depth - 1] < Target)
		{
			// The remaining coins cannot sum up to required target, cut the branch.
			return EvaluationResult.SkipBranch;
		}

		if (depth == selection.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}

	private bool CompareFitness(long includedCoinsCount, long sum, long totalCost)
	{
		// The same amount but less coins. Take it.
		if (sum == BestSelection.PaymentAmount && includedCoinsCount < BestSelection.IncludedCoinsCount)
		{
			return true;
		}
		else if (sum < BestSelection.PaymentAmount || (sum == BestSelection.PaymentAmount && totalCost < BestSelection.TotalCosts))
		{
			// Number of coins is not worse than in the current best selection. Take it.
			if (includedCoinsCount <= BestSelection.IncludedCoinsCount)
			{
				return true;
			}
			else
			{
				// Number of coins is worse. Is it worth it?
				long diff = BestSelection.PaymentAmount - sum;

				// Adding a coin to the selection is warranted when it adds at least 10% of the amount to pay.
				if (diff / (double)BestSelection.PaymentAmount >= 0.1)
				{
					return true;
				}
			}
		}

		return false;
	}
}
