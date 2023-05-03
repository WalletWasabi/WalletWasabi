using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a collection of payments (
public class PaymentBatch
{
	private readonly List<Payment> _payments = new();

	private IEnumerable<PendingPayment> PendingPayments => _payments.OfType<PendingPayment>();
	
	public void AddPendingPayment(PendingPayment payment)
	{
		_payments.Add(payment);
		Logger.LogInfo($"Payment for {payment.Amount} to {payment.Destination.ScriptPubKey.GetDestinationAddress(Network.TestNet)}");
	}

	public PaymentSet GetBestPaymentSet(Money availableAmount, int availableVsize, RoundParameters roundParameters)
	{
		// Not all payments are allowed. Wasabi coordinator only supports P2WPKH and Taproot
		// and even those depend on the round parameters.
		var allowedOutputTypes = roundParameters.AllowedOutputTypes;
		var allowedOutputAmounts = roundParameters.AllowedOutputAmounts;

		var allowedPayments = PendingPayments
			.Where(payment => payment.FitParameters(allowedOutputTypes, allowedOutputAmounts))
			.ToArray();

		// Once we know how much money we have registered in the coinjoin, lets see how many payments
		// we can do we that. Maximum 4 payments in a single coinjoin (arbitrary number)
		var allCombinationOfPayments = allowedPayments.CombinationsWithoutRepetition(1, 4);
		var bestPaymentSet = allCombinationOfPayments
			.Select(paymentSet => new PaymentSet(paymentSet, roundParameters.MiningFeeRate))
			.Where(paymentSet => paymentSet.TotalAmount <= availableAmount)
			.Where(paymentSet => paymentSet.TotalVSize < availableVsize)
			.DefaultIfEmpty(PaymentSet.Empty)
			.MaxBy(x => x.PaymentCount)!;

		LogPaymentSetDetails(bestPaymentSet);
		return bestPaymentSet;
	}

	public InProgressPayment MoveToInProgress(PendingPayment payment)
	{
		if (!_payments.Remove(payment))
		{
			throw new InvalidOperationException("The pending payment was not found.");
		}

		var inProgressPayment = payment.ToInprogressPayment();
		_payments.Add(inProgressPayment);
		return inProgressPayment;
	}
	
	private static void LogPaymentSetDetails(PaymentSet paymentSet)
	{
		Logger.LogInfo($"Best payment set contains {paymentSet.PaymentCount} payments");
		foreach (var payment in paymentSet.Payments)
		{
			Logger.LogInfo($"Id {payment.Id} to {payment.Destination.ScriptPubKey}  {payment.Amount.ToDecimal(MoneyUnit.BTC)}btc");
		}
	}
}
