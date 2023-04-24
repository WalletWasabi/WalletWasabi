using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client;

public class OutputProvider
{
	public OutputProvider(IDestinationProvider destinationProvider)
	{
		DestinationProvider = destinationProvider;
	}

	private IDestinationProvider DestinationProvider { get; }

	public virtual IEnumerable<TxOut> GetOutputs(
		RoundParameters roundParameters,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize,
		WasabiRandom random)
	{
		// Get the output's size and its of the input that will spend it in the future.
		// Here we assume all the outputs share the same scriptpubkey type.
		var isTaprootAllowed = roundParameters.AllowedOutputTypes.Contains(ScriptType.Taproot);

		AmountDecomposer amountDecomposer = new(roundParameters.MiningFeeRate, roundParameters.AllowedOutputAmounts, (int)availableVsize, isTaprootAllowed, random);

		var outputValues = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues).ToArray();
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
