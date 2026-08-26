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
public class PaymentBatch
{
	/// Time to wait before considering an uncertain payment as failed.
	/// This should be longer than CoinRefrigerator's freeze time
	private static readonly TimeSpan UncertainPaymentTimeout = TimeSpan.FromMinutes(3);

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


	/// Marks as finished all the payments that are part of the given coinjoin transaction.
	///
	/// Signed payments have to be finished here too. The transaction is signed before the round ends,
	/// so by the time a round is known to be successful its payments are not in-progress anymore
	public void MovePaymentsToFinished(uint256 txId)
	{
		MovePaymentsTo(InProgressPayments, payment => payment with { State = new FinishedPayment(payment.State, txId) });
		MovePaymentsTo(
			SignedPayments.Where(payment => ((SignedUnknownPayment)payment.State).TransactionId == txId),
			payment => payment with { State = new FinishedPayment(payment.State, txId) });
	}


	public void MoveUnsignedPaymentsToPending() =>
		MovePaymentsTo(InProgressPayments, payment => payment with { State = new PendingPayment(payment.State) });

	public void MoveFailedRoundPaymentsToPending(uint256 roundId)
	{
		MovePaymentsTo(
			InProgressPayments.Where(payment => ((InProgressPayment)payment.State).RoundId == roundId),
			payment => payment with { State = new PendingPayment(payment.State) });
		MovePaymentsTo(
			SignedPayments.Where(payment => ((SignedUnknownPayment)payment.State).RoundId == roundId),
			payment => payment with { State = new PendingPayment(payment.State) });
	}

	public void MovePaymentsToSigned(uint256 transactionId) =>
		MovePaymentsTo(InProgressPayments, payment => payment with
		{
			State = new SignedUnknownPayment(payment.State, DateTimeOffset.UtcNow, ((InProgressPayment)payment.State).RoundId, transactionId)
		});

	public bool TryResolvePaymentsWithTransaction(SmartTransaction transaction)
	{
		var txId = transaction.GetHash();

		// The whole operation is done under the lock. Selecting the payments outside of it would
		// allow another thread to move them in the meantime, and the payment would end up added twice
		lock (_syncObj)
		{
			var resolvedPayments = _payments
				.Where(payment => payment.State is SignedUnknownPayment signed && signed.TransactionId == txId)
				.ToArray();

			foreach (var payment in resolvedPayments)
			{
				Logger.LogInfo($"Payment {payment.Id} resolved as successful - transaction {txId} confirmed.");
				_payments.Remove(payment);
				_payments.Add(payment with { State = new FinishedPayment(payment.State, txId) });
			}

			return resolvedPayments.Length > 0;
		}
	}

	/// <summary>
	/// Moves uncertain payments back to pending state if they have timed out.
	/// This should be called periodically to handle cases where the coinjoin failed
	/// but no matching transaction was ever found.
	/// </summary>
	public void TimeoutUncertainPayments()
	{
		lock (_syncObj)
		{
			var uncertainPayments = _payments.Where(payment => payment.State is SignedUnknownPayment).ToArray();
			foreach (var payment in uncertainPayments)
			{
				var uncertainState = (SignedUnknownPayment)payment.State;
				var elapsed = DateTimeOffset.UtcNow - uncertainState.Timestamp;
				if (elapsed >= UncertainPaymentTimeout)
				{
					Logger.LogInfo($"Payment {payment.Id} timed out after {elapsed.TotalSeconds:F0}s - no matching transaction found, moving back to pending.");
					_payments.Remove(payment);
					_payments.Add(payment with { State = new PendingPayment(uncertainState) });
				}
			}
		}
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
				var movedPayment = move(payment);
				_payments.Remove(payment);
				_payments.Add(movedPayment);
				Logger.LogInfo($"Payment {payment.Id} moved from {payment.State.GetType().Name} to {movedPayment.State.GetType().Name}.");
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
