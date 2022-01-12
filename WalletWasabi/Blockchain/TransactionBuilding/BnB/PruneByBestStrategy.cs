namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that stores the best solution found along the way and prunes
/// search space where we would find worse solutions that we already have.
/// </summary>
public class PruneByBestStrategy : ISearchStrategy
{
	private long _bestTargetSoFar = long.MaxValue;
	private long[]? _bestSolutionSoFar;

	public PruneByBestStrategy(long target)
	{
		Target = target;
	}

	/// <inheritdoc/>
	public long Target { get; }

	public long[]? GetBestSolution() => _bestSolutionSoFar;

	/// <inheritdoc/>
	public EvaluationResult Evaluate(long[] solution, int depth, long effValue)
	{
		if (effValue > _bestTargetSoFar)
		{
			// Our solution is already better than what we might get here.
			return EvaluationResult.SkipBranch;
		}
		else if (effValue >= Target)
		{
			if (_bestTargetSoFar > effValue)
			{
				_bestTargetSoFar = effValue;
				_bestSolutionSoFar = solution[0..depth];
			}

			if (effValue > Target)
			{
				// Excessive funds, cut the branch!
				return EvaluationResult.SkipBranch;
			}
			else
			{
				// Match found!
				return EvaluationResult.Match;
			}
		}
		else if (depth + 1 == solution.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}
}
