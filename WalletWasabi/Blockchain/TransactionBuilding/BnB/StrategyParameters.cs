namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Parameters for BnB strategies.
/// </summary>
public record StrategyParameters
{
	/// <summary>Target value (in satoshis) we want to, ideally, sum up from the input values.</summary>
	public long Target { get; }

	/// <summary>Values in satoshis of the coins the user has (in descending order).</summary>
	public long[] InputValues { get; }

	/// <summary>Costs of spending coins in satoshis.</summary>
	/// <remarks>Costs corresponding to <see cref="InputValues"/> values. So the arrays has to have the same lengths.</remarks>
	public long[] InputCosts { get; }

	/// <param name="target">Target value (in satoshis) we want to, ideally, sum up from the input values.</param>
	/// <param name="inputValues">Values in satoshis of the coins the user has (in descending order).</param>
	/// <param name="inputCosts">Costs of spending coins in satoshis.</param>
	public StrategyParameters(long target, long[] inputValues, long[] inputCosts)
	{
		if (inputValues.Length != inputCosts.Length)
		{
			throw new ArgumentException("Arrays' lengths are not the same.");
		}

		Target = target;
		InputValues = inputValues;
		InputCosts = inputCosts;
	}
}
