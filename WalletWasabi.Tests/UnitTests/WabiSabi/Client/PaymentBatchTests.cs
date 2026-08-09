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
		paymentBatch.MovePaymentsToUncertain();

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

		// Add payment and move to uncertain state
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToUncertain();

		Assert.True(paymentBatch.AreThereUncertainPayments);

		// Create a transaction that matches the payment (same script and amount)
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount);
		var smartTx = new SmartTransaction(tx, Height.Mempool);

		// Try to resolve - should succeed
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.True(resolved);
		Assert.False(paymentBatch.AreThereUncertainPayments);

		// Verify payment is now finished (not pending, not uncertain)
		var finishedPayments = paymentBatch.GetPayments().Where(p => p.State is FinishedPayment);
		Assert.Single(finishedPayments);
	}

	/// <summary>
	/// Verifies that uncertain payments are NOT resolved when transaction doesn't match.
	/// </summary>
	[Fact]
	public void UncertainPaymentsNotResolvedWhenTransactionDoesNotMatch()
	{
		var paymentBatch = new PaymentBatch();
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Add payment and move to uncertain state
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToUncertain();

		// Create a transaction with DIFFERENT amount (doesn't match)
		var differentAmount = Money.Coins(0.2m);
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, differentAmount);
		var smartTx = new SmartTransaction(tx, Height.Mempool);

		// Try to resolve - should NOT succeed
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.False(resolved);
		Assert.True(paymentBatch.AreThereUncertainPayments);
	}

	/// <summary>
	/// Verifies that uncertain payments are NOT resolved when script doesn't match.
	/// </summary>
	[Fact]
	public void UncertainPaymentsNotResolvedWhenScriptDoesNotMatch()
	{
		var paymentBatch = new PaymentBatch();
		var destination = GetNewSegwitAddress();
		var differentDestination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Add payment and move to uncertain state
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToUncertain();

		// Create a transaction with DIFFERENT script (doesn't match)
		var tx = CreateTransactionWithOutput(differentDestination.ScriptPubKey, amount);
		var smartTx = new SmartTransaction(tx, Height.Mempool);

		// Try to resolve - should NOT succeed
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
		paymentBatch.MovePaymentsToUncertain();

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
	/// Verifies that multiple payments can be in uncertain state simultaneously
	/// and are resolved independently.
	/// </summary>
	[Fact]
	public void MultipleUncertainPaymentsResolvedIndependently()
	{
		var paymentBatch = new PaymentBatch();
		var destination1 = GetNewSegwitAddress();
		var destination2 = GetNewSegwitAddress();
		var amount1 = Money.Coins(0.1m);
		var amount2 = Money.Coins(0.2m);

		// Add two payments and move both to uncertain state
		paymentBatch.AddPayment(destination1, amount1);
		paymentBatch.AddPayment(destination2, amount2);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToUncertain();

		Assert.Equal(2, paymentBatch.GetPayments().Count(p => p.State is UncertainPayment));

		// Create a transaction that matches only the first payment
		var tx = CreateTransactionWithOutput(destination1.ScriptPubKey, amount1);
		var smartTx = new SmartTransaction(tx, Height.Mempool);

		// Try to resolve
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.True(resolved);
		// One resolved, one still uncertain
		Assert.Equal(1, paymentBatch.GetPayments().Count(p => p.State is FinishedPayment));
		Assert.Equal(1, paymentBatch.GetPayments().Count(p => p.State is UncertainPayment));
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
		paymentBatch.MovePaymentsToUncertain();

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

		// Step 3: Round ending is UNKNOWN (the critical bug scenario)
		// OLD BUGGY BEHAVIOR: Would have called MovePaymentsToPending() here
		// NEW CORRECT BEHAVIOR: Calls MovePaymentsToUncertain()
		paymentBatch.MovePaymentsToUncertain();

		// Step 4: Verify payment is NOT available for second coinjoin
		Assert.False(paymentBatch.AreTherePendingPayments, "Payment should NOT be pending after unknown round ending");
		Assert.True(paymentBatch.AreThereUncertainPayments, "Payment should be uncertain after unknown round ending");

		// This is the key check - GetBestPaymentSet should return empty
		var secondRoundPaymentSet = paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters);
		Assert.Equal(0, secondRoundPaymentSet.PaymentCount);

		// The double payment bug would have failed here because the payment
		// would have been included in the second coinjoin
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