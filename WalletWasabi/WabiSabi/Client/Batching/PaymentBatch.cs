using System.Collections.ObjectModel;
using WalletWasabi.Blockchain.TransactionOutputs;
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
// A payment that was signed but not broadcast goes back to pending too, and the signed transaction
// stays in its history as a failed attempt. The payment is then only made in a transaction that also
// spends an input of each failed attempt that can still confirm, so that it cannot be made twice.
public class PaymentBatch(CoinsRegistry coins)
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

	public PaymentSet GetBestPaymentSet(Money availableAmount, int availableVsize, RoundParameters roundParameters, ImmutableArray<OutPoint> registeredInputs)
	{
		// Not all payments are allowed. Wasabi coordinator only supports P2WPKH and Taproot
		// and even those depend on the round parameters.
		var allowedOutputTypes = roundParameters.AllowedOutputTypes;
		var allowedOutputAmounts = roundParameters.AllowedOutputAmounts;

		var allowedPayments = PendingPayments
			.Where(payment => payment.FitParameters(allowedOutputTypes, allowedOutputAmounts))
			.ToArray();

		var retryInputs = registeredInputs.ToHashSet();
		foreach (var payment in allowedPayments.Where(payment => !CanBeRetriedWith(payment, retryInputs)))
		{
			Logger.LogInfo($"Payment {payment.Id} is postponed: none of the inputs of an earlier signed transaction paying it is registered in this round.");
		}
		allowedPayments = allowedPayments.Where(payment => CanBeRetriedWith(payment, retryInputs)).ToArray();

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

	// Selecting and moving under the same lock, so the selected payments cannot change in between.
	public PaymentSet MoveBestPaymentSetToInProgress(Money availableAmount, int availableVsize, RoundParameters roundParameters, ImmutableArray<OutPoint> registeredInputs, uint256 roundId)
	{
		lock (_syncObj)
		{
			var bestPaymentSet = GetBestPaymentSet(availableAmount, availableVsize, roundParameters, registeredInputs);
			MovePaymentsToInProgress(bestPaymentSet.Payments, roundId);
			return bestPaymentSet;
		}
	}

	public IEnumerable<Payment> MovePaymentsToInProgress(IEnumerable<Payment> payments, uint256 roundId)
	{
		MovePaymentsTo(payments, payment => payment with { State = new InProgressPayment(payment.State, roundId) });
		return InProgressPayments;
	}

	public void MovePaymentsToFinished(uint256 txId) =>
		MovePaymentsTo(SignedPayments.Where(p => ((SignedUnknownPayment)p.State).TransactionId == txId), payment => payment with { State = new FinishedPayment(payment.State, txId) });

	public void MovePaymentsToPending() =>
		MovePaymentsTo(InProgressPayments, payment => payment with { State = new PendingPayment(payment.State) });

	public void MoveSignedPaymentsToPending() =>
		MovePaymentsTo(SignedPayments, payment => payment with { State = new PendingPayment(payment.State) });

	public void MovePaymentsToSigned(uint256 transactionId, ImmutableArray<OutPoint> inputs) =>
		MovePaymentsTo(InProgressPayments, payment => payment with
		{
			State = new SignedUnknownPayment(payment.State, DateTimeOffset.UtcNow, transactionId, inputs)
		});

	public bool TryResolvePaymentsWithTransaction(SmartTransaction transaction)
	{
		var txId = transaction.GetHash();

		lock (_syncObj)
		{
			var paidPayments = _payments.Where(p => p.State is not FinishedPayment && p.SignedAttempts.Any(a => a.TransactionId == txId)).ToArray();
			foreach (var payment in paidPayments)
			{
				Logger.LogInfo($"Payment {payment.Id} resolved as successful - transaction {txId} seen.");
				_payments.Remove(payment);
				_payments.Add(payment with { State = new FinishedPayment(payment.State, txId) });
			}

			return paidPayments.Length != 0;
		}
	}

	public bool AreTherePendingPayments => PendingPayments.Any();

	public bool AreThereUncertainPayments => SignedPayments.Any();

	private bool CanBeRetriedWith(Payment payment, IReadOnlySet<OutPoint> inputs) =>
		payment.SignedAttempts.Where(CanStillConfirm).All(attempt => attempt.Inputs.Any(inputs.Contains));

	private bool CanStillConfirm(SignedUnknownPayment attempt) =>
		!attempt.Inputs.Any(input =>
			coins.TryGetByOutPoint(input, out var coin)
			&& coin.SpenderTransaction is { Confirmed: true } spender
			&& spender.GetHash() != attempt.TransactionId);

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
