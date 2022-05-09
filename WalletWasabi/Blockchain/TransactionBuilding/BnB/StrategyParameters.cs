namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Parameters for BnB strategies.
/// </summary>
public record StrategyParameters
{
	/// <param name="target">Target value (in satoshis) we want to, ideally, sum up from the input values.</param>
	/// <param name="inputValues">Values in satoshis of the coins the user has (in descending order).</param>
	/// <param name="inputCosts">Costs of spending coins in satoshis.</param>
	/// <param name="maxInputCount">Optionally, maximum number of coins that can be included in a selection, or "infinity".</param>
	public StrategyParameters(long target, long[] inputValues, long[] inputCosts, int maxInputCount = int.MaxValue)
	{
		if (inputValues.Length != inputCosts.Length)
		{
			throw new ArgumentException("Arrays' lengths are not the same.");
		}

		Target = target;
		InputValues = inputValues;
		InputCosts = inputCosts;
		MaxInputCount = maxInputCount;
	}

	/// <summary>Target value (in satoshis) we want to, ideally, sum up from the input values.</summary>
	public long Target { get; }

	/// <summary>Values in satoshis of the coins the user has (in descending order).</summary>
	public long[] InputValues { get; }

	/// <summary>Costs of spending coins in satoshis.</summary>
	/// <remarks>Costs corresponding to <see cref="InputValues"/> values. So the arrays has to have the same lengths.</remarks>
	public long[] InputCosts { get; }

	/// <summary>Maximum number of coins that can be included in a selection.</summary>
	public int MaxInputCount { get; }
}
