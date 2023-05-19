using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a collection of payments. 
// It is possible to add new (pending) payments to be embedded in a coinjoin.
//
// This class is able to select the best set of pending payments that can be done in
// the ongoing coinjoin round based on how much money was registered in it. The set
// of chosen set of payments is moved to in-progress state.
//
// Depending on whether a set of payments is done successfully or not all its belonging
// payments are moved to finished or back to pending state.
public class PaymentBatch
{
	private readonly List<Payment> _payments = new();
	private readonly object _syncObj = new();
	private IEnumerable<PendingPayment> PendingPayments => GetPayments().OfType<PendingPayment>();
	private IEnumerable<InProgressPayment> InProgressPayments => GetPayments().OfType<InProgressPayment>();
	
	public Guid AddPayment(IDestination destination, Money amount)
	{
		var payment = new PendingPayment(destination, amount);
		lock (_syncObj)
		{
			_payments.Add(payment);
		}
		Logger.LogInfo($"Payment for {payment.Amount} to {payment.Destination.ScriptPubKey}");
		return payment.Id;
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
		var allCombinationOfPendingPayments = allowedPayments.CombinationsWithoutRepetition(1, 4);
		var bestPaymentSet = allCombinationOfPendingPayments
			.Select(pandingPaymentSet => new PaymentSet(pandingPaymentSet, roundParameters.MiningFeeRate))
			.Where(paymentSet => paymentSet.TotalAmount <= availableAmount)
			.Where(paymentSet => paymentSet.TotalVSize < availableVsize)
			.DefaultIfEmpty(PaymentSet.Empty)
			.MaxBy(x => x.PaymentCount)!;

		LogPaymentSetDetails(bestPaymentSet);
		return bestPaymentSet;
	}

	public IEnumerable<InProgressPayment> MovePaymentsToInProgress(IEnumerable<PendingPayment> payments, uint256 roundId)
	{
		MovePaymentsTo(payments, p => p.ToInprogressPayment(roundId));
		return InProgressPayments;
	}

	public void MovePaymentsToFinished(uint256 txId) =>
		MovePaymentsTo(InProgressPayments, p => p.ToFinished(txId));

	public void MovePaymentsToPending() =>
		MovePaymentsTo(InProgressPayments, p => p.ToPending());
	
	private void MovePaymentsTo<TOldState, TNewState>(
		IEnumerable<TOldState> payments, 
		Func<TOldState, TNewState> move) where TOldState : Payment where TNewState : Payment
	{
		lock (_syncObj)
		{
			var paymentsToMove = payments.ToArray();
			foreach (var payment in paymentsToMove)
			{
				_payments.Remove(payment);
				_payments.Add(move (payment));
			}
		}
	}
	
	private List<Payment> GetPayments()
	{
		lock (_syncObj)
		{
			return new List<Payment>(_payments);
		}
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
