using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents an `OutputProvider` that has a reference to the `_batchedPayments` instance created by a `Wallet`.
// This class is then aware of the existence of payments for a `Wallet`, what allows it to provide outputs for
// coinjoin round which include those outputs that are payments.
public class PaymentAwareOutputProvider : OutputProvider
{
	public PaymentAwareOutputProvider(IDestinationProvider destinationProvider, PaymentBatch batchedPayments, WasabiRandom? random = null)
		: base(destinationProvider, random)
	{
		_batchedPayments = batchedPayments;
	}

	private readonly PaymentBatch _batchedPayments;

	public override IEnumerable<TxOut> GetOutputs(
		uint256 roundId,
		RoundParameters roundParameters,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize)
	{
		// Get the best combination of payments that can be done with the current amount
		// registered in the round.
		var registeredValues = registeredCoinEffectiveValues.ToArray();
		var availableAmount = registeredValues.Sum();
		var bestPaymentSet = _batchedPayments.GetBestPaymentSet(availableAmount, availableVsize, roundParameters);

		// Return the payments.
		foreach (var payment in bestPaymentSet.Payments)
		{
			yield return payment.ToTxOut();
		}

		_batchedPayments.MovePaymentsToInProgress(bestPaymentSet.Payments, roundId);
		availableVsize -= bestPaymentSet.TotalVSize;
		availableAmount -= bestPaymentSet.TotalAmount;

		// Decompose and return the rest. But before doing that it is important to minimize the impact
		// on the AmountDecomposer by removing those coins that sum enough to make the payments.
		var orderedValues = registeredValues.OrderDescending().ToArray();
		var usedValues = orderedValues
			.Scan(Money.Zero, (acc, current) => acc + current)
			.Zip(orderedValues, (s, v) => (Value: v, Sum: s))
			.TakeUntil(x => x.Sum > bestPaymentSet.TotalAmount)
			.Select(x => x.Value)
			.ToArray();

		var availableValues = orderedValues.Skip(usedValues.Length);

		// in case we over consumed money we reintroduce a virtual coin for the difference.
		var totalValueUsedForPayment = availableAmount - availableValues.Sum();
		if (totalValueUsedForPayment > 0L)
		{
			availableValues = availableValues.Append(totalValueUsedForPayment).ToArray();
		}

		var allCoinEffectiveValues = theirCoinEffectiveValues.Concat(registeredCoinEffectiveValues);

		// Decompose the available values and return them.
		AmountDecomposer amountDecomposer = new(
			roundParameters.MiningFeeRate,
			roundParameters.CalculateMinReasonableOutputAmount(DestinationProvider.SupportedScriptTypes),
			roundParameters.AllowedOutputAmounts.Max,
			availableVsize,
			DestinationProvider.SupportedScriptTypes,
			Random);

		var outputValues = amountDecomposer.Decompose(availableValues.Sum(), allCoinEffectiveValues, bestPaymentSet.TotalAmount > Money.Zero).ToArray();
		var decomposedOutputs = GetTxOuts(outputValues, DestinationProvider);
		foreach (var txOut in decomposedOutputs)
		{
			yield return txOut;
		}
	}
}
