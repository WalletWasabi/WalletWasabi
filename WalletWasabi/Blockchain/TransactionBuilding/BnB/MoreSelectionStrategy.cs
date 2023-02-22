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
	public MoreSelectionStrategy(StrategyParameters parameters, double maxExtraPayment = MaxExtraPayment)
		: base(parameters, new CoinSelection(long.MaxValue, long.MaxValue))
	{
		MaximumTarget = (long)(parameters.Target * maxExtraPayment);
	}

	/// <summary>Maximum acceptable target (inclusive).</summary>
	/// <seealso cref="SelectionStrategy.Target"/>
	public long MaximumTarget { get; }

	/// <inheritdoc/>
	public override EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + CurrentInputCosts;

		if (IncludedCoinsCount > Parameters.MaxInputCount)
		{
			// Too many coins in the selection. Cut the branch.
			return EvaluationResult.SkipBranch;
		}

		if (sum > BestSelection.PaymentAmount || sum > MaximumTarget)
		{
			// Our solution is already better than what we might get here.
			return EvaluationResult.SkipBranch;
		}

		if (sum >= Target)
		{
			if (sum < BestSelection.PaymentAmount || (sum == BestSelection.PaymentAmount && totalCost < BestSelection.TotalCosts))
			{
				BestSelection.Update(paymentAmount: sum, totalCost, IncludedCoinsCount, selection[0..depth]);
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
}
