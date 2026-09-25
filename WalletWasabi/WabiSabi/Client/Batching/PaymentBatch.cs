using System.Collections.ObjectModel;
using WalletWasabi.Blockchain.Transactions;

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
//
// A payment that was signed but not broadcast goes back to pending too, remembering the signed
// transaction as a failed attempt. It is then only paid in a transaction that also spends an input
// of each of its failed attempts, so that the payment cannot be made twice.
public class PaymentBatch
{
	private readonly List<Payment> _payments = new();
	private readonly Lock _syncObj = new();
	private IEnumerable<Payment> PendingPayments => GetPayments().Where(p => p.State is PendingPayment);
	private IEnumerable<Payment> InProgressPayments => GetPayments().Where(p => p.State is InProgressPayment);
	private IEnumerable<Payment> SignedPayments => GetPayments().Where(p => p.State is SignedUnknownPayment);

	public Guid AddPayment(IDestination destination, Money amount)
	{
		var payment = new Payment(destination, amount);
		lock (_syncObj)
		{
			_payments.Add(payment);
		}
		Logger.LogInfo($"Payment {payment.Id} for {payment.Amount} BTC to {payment.Destination.ScriptPubKey}.");
		return payment.Id;
	}

	public void AbortPayment(Guid id)
	{
		lock (_syncObj)
		{
			if (_payments.FirstOrDefault(p => p.Id == id) is { } payment)
			{
				if (payment.State is PendingPayment)
				{
					_payments.Remove(payment);
					Logger.LogInfo($"Payment {payment.Id} for {payment.Amount} BTC to {payment.Destination.ScriptPubKey} was canceled.");
				}
				else
				{
					Logger.LogInfo($"Payment {payment.Id} could not be canceled because it is not pending.");
					throw new InvalidOperationException("Payment could not be canceled because it is not pending.");
				}
			}
			else
			{
				Logger.LogInfo($"Payment {id} was not found.");
				throw new InvalidOperationException("Payment was not found.");
			}
		}
	}

	public PaymentSet GetBestPaymentSet(Money availableAmount, int availableVsize, RoundParameters roundParameters, IEnumerable<OutPoint> registeredInputs)
	{
		// Not all payments are allowed. Wasabi coordinator only supports P2WPKH and Taproot
		// and even those depend on the round parameters.
		var allowedOutputTypes = roundParameters.AllowedOutputTypes;
		var allowedOutputAmounts = roundParameters.AllowedOutputAmounts;

		var allowedPayments = PendingPayments
			.Where(payment => payment.FitParameters(allowedOutputTypes, allowedOutputAmounts))
			.ToArray();

		var retryInputs = registeredInputs.ToHashSet();
		foreach (var payment in allowedPayments.Where(payment => !payment.IsProtectedBy(retryInputs)))
		{
			Logger.LogInfo($"Payment {payment.Id} is postponed: none of the inputs of an earlier signed transaction paying it is registered in this round.");
		}
		allowedPayments = allowedPayments.Where(payment => payment.IsProtectedBy(retryInputs)).ToArray();

		// Once we know how much money we have registered in the coinjoin, lets see how many payments
		// we can do we that. Maximum 4 payments in a single coinjoin (arbitrary number)
		var allCombinationOfPendingPayments = allowedPayments.CombinationsWithoutRepetition(1, 4);
		var bestPaymentSet = allCombinationOfPendingPayments
			.Select(pendingPaymentSet => new PaymentSet(pendingPaymentSet, roundParameters.MiningFeeRate))
			.Where(paymentSet => paymentSet.TotalAmount <= availableAmount)
			.Where(paymentSet => paymentSet.TotalAmount == availableAmount // edge case where payments match exactly the available amount
				? paymentSet.TotalVSize <= availableVsize
				: paymentSet.TotalVSize + Math.Max(Constants.P2trOutputVirtualSize, Constants.P2wpkhOutputVirtualSize) <= availableVsize)
			.DefaultIfEmpty(PaymentSet.Empty)
			.MaxBy(x => x.PaymentCount)!;

		LogPaymentSetDetails(bestPaymentSet);
		return bestPaymentSet;
	}

	public IEnumerable<Payment> MovePaymentsToInProgress(IEnumerable<Payment> payments, uint256 roundId)
	{
		MovePaymentsTo(payments, payment => payment with { State = new InProgressPayment(payment.State, roundId) });
		return InProgressPayments;
	}

	public void MovePaymentsToFinished(uint256 txId) =>
		MovePaymentsTo(SignedPayments.Where(p => ((SignedUnknownPayment)p.State).TransactionId == txId), payment => payment with { State = new FinishedPayment(payment.State, txId) });

	// Signed payments go back to pending if the round ends without broadcasting them. They are remembered as failed attempts. 
	public void MovePaymentsToPending()
	{
		MovePaymentsTo(InProgressPayments, payment => payment with { State = new PendingPayment(payment.State) });
		MovePaymentsTo(SignedPayments, payment =>
		{
			var signed = (SignedUnknownPayment)payment.State;
			Logger.LogInfo($"Payment {payment.Id} is pending again. Transaction {signed.TransactionId} was signed but not broadcast, so the payment will only be made in a transaction that conflicts with it.");
			return payment with
			{
				State = new PendingPayment(payment.State),
				FailedAttempts = payment.FailedAttempts.Add(new FailedAttempt(signed.TransactionId, signed.Inputs))
			};
		});
	}

	public void MovePaymentsToSigned(uint256 transactionId, ImmutableArray<OutPoint> inputs) =>
		MovePaymentsTo(InProgressPayments, payment => payment with
		{
			State = new SignedUnknownPayment(payment.State, DateTimeOffset.UtcNow, transactionId, inputs)
		});

	// A transaction retrying the payments must spend at least one input of each group, to prevent double spending.
	public ImmutableArray<ImmutableArray<OutPoint>> GetFailedAttemptInputs() =>
		PendingPayments.SelectMany(p => p.FailedAttempts).Select(a => a.Inputs).ToImmutableArray();

	public bool TryResolvePaymentsWithTransaction(SmartTransaction transaction)
	{
		var resolved = false;
		var txId = transaction.GetHash();
		var spentInputs = transaction.Transaction.Inputs.Select(i => i.PrevOut).ToHashSet();

		lock (_syncObj)
		{
			foreach (var payment in _payments.Where(p => p.State is not FinishedPayment).ToArray())
			{
				if ((payment.State is SignedUnknownPayment s && s.TransactionId == txId) || payment.FailedAttempts.Any(a => a.TransactionId == txId))
				{
					Logger.LogInfo($"Payment {payment.Id} resolved as successful - transaction {txId} seen.");
					_payments.Remove(payment);
					_payments.Add(payment with { State = new FinishedPayment(payment.State, txId) });
					resolved = true;
				}
				else if (transaction.Confirmed && payment.FailedAttempts.Any(a => a.Inputs.Any(spentInputs.Contains)))
				{
					var invalidatedAttempts = payment.FailedAttempts.Where(a => a.Inputs.Any(spentInputs.Contains)).ToArray();
					foreach (var attempt in invalidatedAttempts)
					{
						Logger.LogInfo($"Payment {payment.Id}: transaction {attempt.TransactionId} can no longer confirm, confirmed transaction {txId} spends one of its inputs.");
					}
					_payments.Remove(payment);
					_payments.Add(payment with { FailedAttempts = payment.FailedAttempts.RemoveRange(invalidatedAttempts) });
					resolved = true;
				}
			}
		}

		return resolved;
	}

	public bool AreTherePendingPayments => PendingPayments.Any();

	public bool AreThereUncertainPayments => SignedPayments.Any();

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
				_payments.Add(move(payment));
			}
		}
	}

	public ReadOnlyCollection<Payment> GetPayments()
	{
		lock (_syncObj)
		{
			return _payments.AsReadOnly();
		}
	}

	private static void LogPaymentSetDetails(PaymentSet paymentSet)
	{
		Logger.LogInfo($"Best payment set contains {paymentSet.PaymentCount} payments.");
		foreach (var payment in paymentSet.Payments)
		{
			Logger.LogInfo($"Id {payment.Id} to {payment.Destination.ScriptPubKey}  {payment.Amount.ToDecimal(MoneyUnit.BTC)} BTC.");
		}
	}
}
