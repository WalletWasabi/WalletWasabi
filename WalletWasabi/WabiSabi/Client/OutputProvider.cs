using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;

namespace WalletWasabi.WabiSabi.Client;

public class OutputProvider
{
	public OutputProvider(IDestinationProvider destinationProvider, RandomnessProvider random)
	{
		DestinationProvider = destinationProvider;
		Random = random;
	}

	internal IDestinationProvider DestinationProvider { get; }
	protected RandomnessProvider Random { get; }

	public virtual IEnumerable<TxOut> GetOutputs(
		uint256 roundId,
		RoundParameters roundParameters,
		ImmutableArray<OutPoint> registeredInputs,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize,
		bool arePaymentsAllowed)
	{
		AmountDecomposer amountDecomposer = new(
			roundParameters.MiningFeeRate,
			roundParameters.CalculateMinReasonableOutputAmount(DestinationProvider.SupportedScriptTypes),
			roundParameters.AllowedOutputAmounts.Max,
			availableVsize,
			DestinationProvider.SupportedScriptTypes,
			Random);

		var allCoinEffectiveValues = theirCoinEffectiveValues.Concat(registeredCoinEffectiveValues);
		var outputValues = amountDecomposer.Decompose(registeredCoinEffectiveValues.Sum(), allCoinEffectiveValues).ToArray();
		return GetTxOuts(outputValues);
	}

	/// <summary>
	/// Makes the destinations of the outputs that did not end up in a coinjoin available again.
	/// </summary>
	public void ReleaseDestinations() => DestinationProvider.ReleaseDestinations(holder: this);

	internal IEnumerable<TxOut> GetTxOuts(IEnumerable<Output> outputValues)
	{
		// These outputs are for a new round, so the destinations of the previous one (a failed round before a blame round) are free again.
		ReleaseDestinations();

		// Get as many destinations as outputs we need.
		var taprootOutputCount = outputValues.Count(output => output.ScriptType is ScriptType.Taproot);
		var taprootScripts = new Stack<IDestination>(DestinationProvider.GetNextDestinations(taprootOutputCount, preferTaproot: true, holder: this));
		var segwitOutputCount = outputValues.Count(output => output.ScriptType is ScriptType.P2WPKH);
		var segwitScripts = new Stack<IDestination>(DestinationProvider.GetNextDestinations(segwitOutputCount, preferTaproot: false, holder: this));

		List<TxOut> outputTxOuts = new();
		foreach (var output in outputValues)
		{
			var destinationStack = output.ScriptType is ScriptType.Taproot
				? taprootScripts
				: segwitScripts;

			var destination = destinationStack.Pop();
			var txOut = new TxOut(output.Amount, destination.ScriptPubKey);
			outputTxOuts.Add(txOut);
		}
		return outputTxOuts;
	}
}
