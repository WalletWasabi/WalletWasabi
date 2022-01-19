using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public abstract class BaseStrategy : ISearchStrategy
{
	/// <param name="inputValues">All values must be strictly positive and in descending order.</param>
	public BaseStrategy(long target, long[] inputValues)
	{
		Target = target;

		if (inputValues.Length == 0)
		{
			throw new ArgumentException("List is empty.");
		}

		if (inputValues.Any(x => x <= 0))
		{
			throw new ArgumentException("Only strictly positive values are supported.");
		}

		InputValues = inputValues.OrderByDescending(x => x).ToArray();

		if (!inputValues.SequenceEqual(InputValues))
		{
			throw new ArgumentException("Input values must be sorted in descending order.");
		}
	}

	/// <summary>Target value we want to, ideally, sum up from the input values.</summary>
	public long Target { get; }

	/// <summary>Input values sorted in descending orders.</summary>
	public long[] InputValues { get; }

	/// <inheritdoc/>
	public virtual long ProcessAction(NextAction action, long[] selection, int depth, long oldSum)
	{
		long newSum;

		if (action == NextAction.IncludeFirstThenOmit || action == NextAction.Include)
		{
			selection[depth] = InputValues[depth];
			newSum = oldSum + selection[depth];
		}
		else if (action == NextAction.OmitFirstThenInclude || action == NextAction.Omit)
		{
			newSum = oldSum - selection[depth];
			selection[depth] = 0;
		}
		else
		{
			newSum = oldSum - selection[depth];
			selection[depth] = 0;
		}

		return newSum;
	}

	/// <inheritdoc/>
	public abstract EvaluationResult Evaluate(long[] selection, int count, long sum);
}
