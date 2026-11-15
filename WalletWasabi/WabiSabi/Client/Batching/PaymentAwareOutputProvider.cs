using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

namespace WalletWasabi.WabiSabi.Client.Batching;

/// <summary>
/// Represents an <see cref="OutputProvider">output provider</see> that has a reference to the <see cref="Wallet.BatchedPayments"/> structure.
/// </summary>
/// <remarks>
/// This class is aware of <see cref="Wallet"/>'s coinjoin payments, which allows it to provide outputs for coinjoin rounds that include those outputs that are payments.
/// </remarks>
public class PaymentAwareOutputProvider(
	IDestinationProvider destinationProvider,
	PaymentBatch batchedPayments,
	RandomnessProvider random)
	: OutputProvider(destinationProvider, random)
{
	public override IEnumerable<TxOut> GetOutputs(
		uint256 roundId,
		RoundParameters roundParameters,
		ImmutableArray<OutPoint> registeredInputs,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize,
		bool arePaymentsAllowed)
	{
		if (!arePaymentsAllowed)
		{
			if (batchedPayments.AreTherePendingPayments)
			{
				Logger.LogInfo("There are pending payments but they cannot be funded with non-private coins.");
			}

			return base.GetOutputs(roundId, roundParameters, registeredInputs, registeredCoinEffectiveValues, theirCoinEffectiveValues, availableVsize, arePaymentsAllowed);
		}

		return GetOutputsIncludingPayments(roundId, roundParameters, registeredInputs, registeredCoinEffectiveValues, theirCoinEffectiveValues, availableVsize);
	}

	private IEnumerable<TxOut> GetOutputsIncludingPayments(
		uint256 roundId,
		RoundParameters roundParameters,
		ImmutableArray<OutPoint> registeredInputs,
		IEnumerable<Money> registeredCoinEffectiveValues,
		IEnumerable<Money> theirCoinEffectiveValues,
		int availableVsize)
	{
		// Get the best combination of payments that can be done with the current amount registered in the round.
		var registeredValues = registeredCoinEffectiveValues.ToArray();
		var availableAmount = registeredValues.Sum();
		var bestPaymentSet = batchedPayments.MoveBestPaymentSetToInProgress(availableAmount, availableVsize, roundParameters, registeredInputs, roundId);

		// Return the payments.
		foreach (var payment in bestPaymentSet.Payments)
		{
			yield return payment.ToTxOut();
		}

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
