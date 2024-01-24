using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;

namespace WalletWasabi.Blockchain.TransactionBuilding;

/// <seealso href="https://murch.one/wp-content/uploads/2016/11/erhardt2016coinselection.pdf">Section "5.3 Branch and Bound".</seealso>
public class BranchAndBound
{
	/// <summary>
	/// Attempts to find a set of values that sum up to the target value.
	/// </summary>
	/// <param name="searchStrategy">Search strategy that affects how the algorithm searches through the options.</param>
	/// <param name="selectedValues">Solution of the search algorithm based on <paramref name="searchStrategy"/> sorted in descending order.</param>
	/// <returns><c>true</c> when a match is found, <c>false</c> otherwise.</returns>
	public bool TryGetMatch(SelectionStrategy searchStrategy, [NotNullWhen(true)] out List<long>? selectedValues, CancellationToken cancellationToken)
	{
		selectedValues = null;

		if (TryFindSolution(searchStrategy, out long[]? solution, cancellationToken))
		{
			selectedValues = new List<long>();

			for (int i = 0; i < solution.Length; i++)
			{
				if (solution[i] > 0)
				{
					selectedValues.Add(solution[i]);
				}
			}

			return true;
		}

		return false;
	}

	private bool TryFindSolution(SelectionStrategy searchStrategy, [NotNullWhen(true)] out long[]? selection, CancellationToken cancellationToken)
	{
		// Current selection sum.
		long sum = 0L;

		// Current depth (think of the depth in the recursive algorithm sense).
		int depth = 0;

		int length = searchStrategy.InputValues.Length;

		selection = new long[length];
		NextAction[] actions = new NextAction[length];
		actions[0] = GetRandomNextAction();

		int i = 0;

		do
		{
			i++;
			NextAction action = actions[depth];

			// Branch WITH the value included.
			if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
			{
				actions[depth] = GetNextStep(action);
				sum = searchStrategy.ProcessAction(action, selection, depth, sum);

				EvaluationResult result = searchStrategy.Evaluate(selection, depth + 1, sum);

				if (result == EvaluationResult.SkipBranch)
				{
					continue;
				}
				else if (result == EvaluationResult.Match)
				{
					return true;
				}

				depth++;
				actions[depth] = GetRandomNextAction();
			}
			else if (action == NextAction.OmitFirstThenInclude || action == NextAction.Omit)
			{
				actions[depth] = GetNextStep(action);

				// Branch WITHOUT the value included.
				sum = searchStrategy.ProcessAction(action, selection, depth, sum);

				if (depth + 1 == length)
				{
					// Leaf reached, no match.
					continue;
				}

				depth++;
				actions[depth] = GetRandomNextAction();
			}
			else
			{
				sum = searchStrategy.ProcessAction(action, selection, depth, sum);
				depth--;
			}

			// Micro optimization: Do not check cancellation token every time as it requires accessing volatile memory.
			if (i % 10_000 == 0 && cancellationToken.IsCancellationRequested)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}
		}
		while (depth >= 0);

		return false;
	}

	private NextAction GetRandomNextAction()
	{
		return Random.Shared.Next() > (int.MaxValue / 2) ? NextAction.IncludeFirstThenOmit : NextAction.OmitFirstThenInclude;
	}

	private static NextAction GetNextStep(NextAction action)
	{
		return action switch
		{
			NextAction.IncludeFirstThenOmit => NextAction.Omit,
			NextAction.OmitFirstThenInclude => NextAction.Include,
			NextAction.Include => NextAction.Backtrack,
			NextAction.Omit => NextAction.Backtrack,
			NextAction.Backtrack => throw new InvalidOperationException("This should never happen."),
			_ => throw new InvalidOperationException("No other values are valid.")
		};
	}
}
