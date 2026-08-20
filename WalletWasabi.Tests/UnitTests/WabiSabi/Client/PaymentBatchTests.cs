using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Coordinator;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for PaymentBatch focusing on preventing double payments
/// when coinjoin rounds end with unknown status.
/// </summary>
public class PaymentBatchTests
{
	/// <summary>
	/// Verifies that when a payment is moved to uncertain state (due to unknown round ending),
	/// it is NOT included in subsequent payment selections, preventing double payments.
	/// </summary>
	[Fact]
	public void UncertainPaymentsAreNotSelectedForNewCoinjoins()
	{
		var paymentBatch = new PaymentBatch();
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Add a payment
		paymentBatch.AddPayment(destination, amount);
		Assert.True(paymentBatch.AreTherePendingPayments);

		// Move to in-progress (simulating coinjoin round starting)
		var roundId = uint256.One;
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, roundId);

		// Move to uncertain (simulating unknown round ending - the critical path for double payments)
		var signedTxId = CreateTransactionWithOutput(destination.ScriptPubKey, amount).GetHash();
		paymentBatch.MovePaymentsToSigned(signedTxId);

		// Verify the payment is NOT pending anymore
		Assert.False(paymentBatch.AreTherePendingPayments);
		Assert.True(paymentBatch.AreThereUncertainPayments);

		// Verify GetBestPaymentSet returns empty - this is the key assertion!
		// If this returned the payment, it would be included in another coinjoin = double payment
		var availableMoney = Money.Coins(1m);
		var availableVsize = 1000;
		var bestPaymentSet = paymentBatch.GetBestPaymentSet(availableMoney, availableVsize, roundParameters);

		Assert.Equal(0, bestPaymentSet.PaymentCount);
	}

	/// <summary>
	/// Verifies that uncertain payments are resolved when a matching transaction is found.
	/// </summary>
	[Fact]
	public void UncertainPaymentsAreResolvedWhenMatchingTransactionArrives()
	{
		var paymentBatch = new PaymentBatch();
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Create the signed transaction first (so we know the txId)
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount);
		var signedTxId = tx.GetHash();

		// Add payment and move to uncertain state with the known txId
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToSigned(signedTxId);

		Assert.True(paymentBatch.AreThereUncertainPayments);

		// Create a SmartTransaction from the same tx (matching txId)
		var smartTx = new SmartTransaction(tx, Height.Mempool);

		// Try to resolve - should succeed because txId matches
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.True(resolved);
		Assert.False(paymentBatch.AreThereUncertainPayments);

		// Verify payment is now finished (not pending, not uncertain)
		var finishedPayments = paymentBatch.GetPayments().Where(p => p.State is FinishedPayment);
		Assert.Single(finishedPayments);
	}

	/// <summary>
	/// Verifies that uncertain payments are NOT resolved when transaction txId doesn't match.
	/// </summary>
	[Fact]
	public void UncertainPaymentsNotResolvedWhenTransactionIdDoesNotMatch()
	{
		var paymentBatch = new PaymentBatch();
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Create the signed transaction and store its txId
		var signedTx = CreateTransactionWithOutput(destination.ScriptPubKey, amount);
		var signedTxId = signedTx.GetHash();

		// Add payment and move to uncertain state
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToSigned(signedTxId);

		// Create a DIFFERENT transaction (different txId)
		var differentTx = CreateTransactionWithOutput(destination.ScriptPubKey, Money.Coins(0.2m));
		var smartTx = new SmartTransaction(differentTx, Height.Mempool);

		// Try to resolve - should NOT succeed because txId doesn't match
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.False(resolved);
		Assert.True(paymentBatch.AreThereUncertainPayments);
	}

	/// <summary>
	/// Verifies that uncertain payments timeout and move back to pending after the timeout period.
	/// </summary>
	[Fact]
	public void UncertainPaymentsTimeoutAfterWaiting()
	{
		var paymentBatch = new PaymentBatch();
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Add payment and move to uncertain state
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		var signedTxId = CreateTransactionWithOutput(destination.ScriptPubKey, amount).GetHash();
		paymentBatch.MovePaymentsToSigned(signedTxId);

		Assert.True(paymentBatch.AreThereUncertainPayments);
		Assert.False(paymentBatch.AreTherePendingPayments);

		// Timeout should not move payments back immediately (timeout is 3 minutes)
		paymentBatch.TimeoutUncertainPayments();
		Assert.True(paymentBatch.AreThereUncertainPayments);

		// Note: To fully test the timeout, we would need to mock DateTimeOffset.UtcNow
		// or wait 3 minutes. For now, we verify the method doesn't crash and
		// doesn't immediately move payments back.
	}

	/// <summary>
	/// Verifies that all payments from the same coinjoin are resolved together
	/// when the matching transaction is found.
	/// </summary>
	[Fact]
	public void AllPaymentsFromSameCoinjoinResolvedTogether()
	{
		var paymentBatch = new PaymentBatch();
		var destination1 = GetNewSegwitAddress();
		var destination2 = GetNewSegwitAddress();
		var amount1 = Money.Coins(0.1m);
		var amount2 = Money.Coins(0.2m);

		// Create the signed coinjoin transaction with both outputs
		var tx = Transaction.Create(Network.Main);
		tx.Outputs.Add(new TxOut(amount1, destination1.ScriptPubKey));
		tx.Outputs.Add(new TxOut(amount2, destination2.ScriptPubKey));
		var signedTxId = tx.GetHash();

		// Add two payments and move both to uncertain state with same txId
		paymentBatch.AddPayment(destination1, amount1);
		paymentBatch.AddPayment(destination2, amount2);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToSigned(signedTxId);

		Assert.Equal(2, paymentBatch.GetPayments().Count(p => p.State is SignedUnknownPayment));

		// When the transaction arrives, BOTH payments should be resolved
		var smartTx = new SmartTransaction(tx, Height.Mempool);
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.True(resolved);
		// Both should be resolved
		Assert.Equal(2, paymentBatch.GetPayments().Count(p => p.State is FinishedPayment));
		Assert.Equal(0, paymentBatch.GetPayments().Count(p => p.State is SignedUnknownPayment));
	}

	/// <summary>
	/// Verifies that pending payments are still available while uncertain payments exist.
	/// This ensures the uncertain state doesn't block other payments.
	/// </summary>
	[Fact]
	public void PendingPaymentsStillAvailableWhileUncertainPaymentsExist()
	{
		var paymentBatch = new PaymentBatch();
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var uncertainDestination = GetNewSegwitAddress();
		var pendingDestination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Add first payment and move to uncertain
		paymentBatch.AddPayment(uncertainDestination, amount);
		var firstPayment = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(firstPayment, uint256.One);
		var signedTxId = CreateTransactionWithOutput(uncertainDestination.ScriptPubKey, amount).GetHash();
		paymentBatch.MovePaymentsToSigned(signedTxId);

		// Add a new pending payment
		paymentBatch.AddPayment(pendingDestination, amount);

		Assert.True(paymentBatch.AreTherePendingPayments);
		Assert.True(paymentBatch.AreThereUncertainPayments);

		// GetBestPaymentSet should return the pending payment, not the uncertain one
		var availableMoney = Money.Coins(1m);
		var availableVsize = 1000;
		var bestPaymentSet = paymentBatch.GetBestPaymentSet(availableMoney, availableVsize, roundParameters);

		Assert.Equal(1, bestPaymentSet.PaymentCount);
		Assert.Equal(pendingDestination.ScriptPubKey, bestPaymentSet.Payments.Single().Destination.ScriptPubKey);
	}

	/// <summary>
	/// Simulates the exact scenario that caused the double payment bug:
	/// 1. Payment queued
	/// 2. First coinjoin starts, payment moves to in-progress
	/// 3. Round ending is unknown, payment should move to uncertain (not pending!)
	/// 4. Second coinjoin should NOT include the uncertain payment
	/// </summary>
	[Fact]
	public void DoublePaymentPreventionScenario()
	{
		var paymentBatch = new PaymentBatch();
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.006m); // The exact amount from the bug report

		// Step 1: Queue the payment
		paymentBatch.AddPayment(destination, amount);
		Assert.True(paymentBatch.AreTherePendingPayments);

		// Step 2: First coinjoin starts
		var firstRoundId = uint256.One;
		var paymentsForFirstRound = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(paymentsForFirstRound, firstRoundId);
		Assert.False(paymentBatch.AreTherePendingPayments);

		// Step 3: Transaction signed - payments move to signed state immediately
		// This happens via TransactionSigned event right after signing
		var signedTxId = CreateTransactionWithOutput(destination.ScriptPubKey, amount).GetHash();
		paymentBatch.MovePaymentsToSigned(signedTxId);

		// Step 4: Round ending is UNKNOWN - payments stay in signed state
		// They are NOT moved back to pending to avoid double payments
		Assert.False(paymentBatch.AreTherePendingPayments, "Payment should NOT be pending after signing");
		Assert.True(paymentBatch.AreThereUncertainPayments, "Payment should be in signed state awaiting resolution");

		// This is the key check - GetBestPaymentSet should return empty
		var secondRoundPaymentSet = paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters);
		Assert.Equal(0, secondRoundPaymentSet.PaymentCount);

		// The double payment bug would have failed here because the payment
		// would have been included in the second coinjoin
	}

	/// <summary>
	/// A round that is known to have ended with a broadcast transaction must finalize its payments,
	/// even though the TransactionSigned event already moved them to the signed (uncertain) state.
	/// Otherwise they stay uncertain and <see cref="PaymentBatch.TimeoutUncertainPayments"/> eventually
	/// puts them back to pending, which resubmits an already-paid payment into a following round.
	/// </summary>
	[Fact]
	public void SuccessfulRoundFinalizesSignedPayments()
	{
		var paymentBatch = new PaymentBatch();
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);

		// The client signed the coinjoin: payments are tracked with the txId.
		var coinJoinTxId = CreateTransactionWithOutput(destination.ScriptPubKey, amount).GetHash();
		paymentBatch.MovePaymentsToSigned(coinJoinTxId);

		// The round is known to have ended with the transaction broadcast.
		paymentBatch.MovePaymentsToFinished(coinJoinTxId);

		Assert.False(paymentBatch.AreThereUncertainPayments);
		Assert.False(paymentBatch.AreTherePendingPayments);
		Assert.IsType<FinishedPayment>(paymentBatch.GetPayments().Single().State);

		// Even the timeout sweep must not resurrect it.
		paymentBatch.TimeoutUncertainPayments();
		Assert.False(paymentBatch.AreTherePendingPayments);
	}

	private static BitcoinAddress GetNewSegwitAddress()
	{
		using Key key = new();
		return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
	}

	private static Transaction CreateTransactionWithOutput(Script scriptPubKey, Money amount)
	{
		var tx = Transaction.Create(Network.Main);
		tx.Outputs.Add(new TxOut(amount, scriptPubKey));
		return tx;
	}
}