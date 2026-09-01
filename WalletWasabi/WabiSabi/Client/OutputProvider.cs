using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.Crypto.Randomness;

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

	/// <param name="arePaymentsAllowed">
	/// Whether the round is allowed to fund batched payments. Only meaningful for
	/// <see cref="Batching.PaymentAwareOutputProvider"/>; plain decomposition ignores it.
	/// </param>
	public virtual IEnumerable<TxOut> GetOutputs(
		uint256 roundId,
		RoundParameters roundParameters,
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
		return GetTxOuts(outputValues, DestinationProvider);
	}

	internal static IEnumerable<TxOut> GetTxOuts(IEnumerable<Output> outputValues, IDestinationProvider destinationProvider)
	{
		// Get as many destinations as outputs we need.
		var taprootOutputCount = outputValues.Count(output => output.ScriptType is ScriptType.Taproot);
		var taprootScripts = new Stack<IDestination>(destinationProvider.GetNextDestinations(taprootOutputCount, preferTaproot: true));
		var segwitOutputCount = outputValues.Count(output => output.ScriptType is ScriptType.P2WPKH);
		var segwitScripts = new Stack<IDestination>(destinationProvider.GetNextDestinations(segwitOutputCount, preferTaproot: false));

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
