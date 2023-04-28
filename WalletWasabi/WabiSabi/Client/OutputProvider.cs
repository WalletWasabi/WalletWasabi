using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Client.Batching;

namespace WalletWasabi.WabiSabi.Client;

public class OutputProvider
{
	public OutputProvider(IDestinationProvider destinationProvider, WasabiRandom? random = null)
	{
		DestinationProvider = destinationProvider;
		Random = random ?? SecureRandom.Instance;
	}

	private IDestinationProvider DestinationProvider { get; }
	private WasabiRandom Random { get; }

	public virtual IEnumerable<TxOut> GetOutputs(
		RoundParameters roundParameters,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize)
	{
		AmountDecomposer amountDecomposer = new(roundParameters.MiningFeeRate, roundParameters.CalculateMinReasonableOutputAmount(), roundParameters.AllowedOutputAmounts.Max, availableVsize, roundParameters.AllowedOutputTypes, Random);

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

public class PaymentAwareOutputProvider : OutputProvider
{
	public PaymentAwareOutputProvider(IDestinationProvider destinationProvider)
		: base(destinationProvider)
	{
	}

	private List<PendingPayment> _pendingPayments = new();
	public IReadOnlyList<PendingPayment> PendingPayments => _pendingPayments;

	public void AddPendingPayment(PendingPayment payment) =>
		_pendingPayments.Add(payment);

	public override IEnumerable<TxOut> GetOutputs(
		RoundParameters roundParameters,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize)
	{
		var registeredValues = registeredCoinEffectiveValues.ToArray();
		var availableAmount = registeredValues.Sum();

		// Not all payments are allowed. Wasabi coordinator only supports P2WPKH and Taproot
		// and even those depend on the round parameters.
		var allowedOutputTypes = roundParameters.AllowedOutputTypes;
		var allowedOutputAmounts = roundParameters.AllowedOutputAmounts;
		var allowedPayments = PendingPayments
			.Where(x => allowedOutputTypes.Contains(x.Destination.ScriptPubKey.GetScriptType()) && allowedOutputAmounts.Contains(x.Amount));

		// Once we know how much money we have registered in the coinjoin, lets see how many payments
		// we can do we that. Maximum 4 payments in a single coinjoin (arbitrary number)
		var allCombinationOfPayments = allowedPayments.CombinationsWithoutRepetition(1, 4);
		var bestPaymentSet = allCombinationOfPayments
			.Select(paymentSet => new PaymentSet(paymentSet, roundParameters.MiningFeeRate))
			.Where(paymentSet => paymentSet.TotalAmount <= availableAmount)
			.Where(paymentSet => paymentSet.TotalVSize < availableVsize)
			.DefaultIfEmpty(PaymentSet.Empty)
			.MaxBy(x => x.PaymentCount)!;

		// Return the payments.
		foreach (var payment in bestPaymentSet.Payments)
		{
			yield return payment.ToTxOut();
		}
		availableVsize -= bestPaymentSet.TotalVSize;

		// Decompose and return the rest. But before doing that it is important to minimize the impact
		// on the AmountDecomposer by removing those coins that sum enough to make the payments.
		var orderedValues = registeredValues.OrderDescending().ToArray();
		var availableValues = orderedValues
			.Scan(Money.Zero, (acc, current) => acc + current)
			.Zip(orderedValues, (v, s) => (Value: v, Sum: s))
			.SkipWhile(x => x.Sum < bestPaymentSet.TotalAmount)
			.Select(x => x.Value)
			.ToArray();

		// in case we over consumed money we reintroduce a virtual coin for the the difference.
		var totalValueUsedForPayment = availableAmount - availableValues.Sum();
		if (totalValueUsedForPayment > bestPaymentSet.TotalAmount)
		{
			availableValues = availableValues.Append(totalValueUsedForPayment - bestPaymentSet.TotalAmount).ToArray();
		}

		// Decompose the available values and return them.
		var decomposedOutputs = base.GetOutputs(roundParameters, availableValues, theirCoinEffectiveValues, availableVsize);
		foreach (var txOut in decomposedOutputs)
		{
			yield return txOut;
		}
	}
}
