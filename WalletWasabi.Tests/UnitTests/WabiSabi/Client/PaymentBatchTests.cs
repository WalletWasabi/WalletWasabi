using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
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
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
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
		paymentBatch.MovePaymentsToSigned(signedTxId, []);

		// Verify the payment is NOT pending anymore
		Assert.False(paymentBatch.AreTherePendingPayments);
		Assert.True(paymentBatch.AreThereUncertainPayments);

		// Verify GetBestPaymentSet returns empty - this is the key assertion!
		// If this returned the payment, it would be included in another coinjoin = double payment
		var availableMoney = Money.Coins(1m);
		var availableVsize = 1000;
		var bestPaymentSet = paymentBatch.GetBestPaymentSet(availableMoney, availableVsize, roundParameters, registeredInputs: []);

		Assert.Equal(0, bestPaymentSet.PaymentCount);
	}

	/// <summary>
	/// Verifies that uncertain payments are resolved when a matching transaction is found.
	/// </summary>
	[Fact]
	public void UncertainPaymentsAreResolvedWhenMatchingTransactionArrives()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Create the signed transaction first (so we know the txId)
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount);
		var signedTxId = tx.GetHash();

		// Add payment and move to uncertain state with the known txId
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToSigned(signedTxId, []);

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
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Create the signed transaction and store its txId
		var signedTx = CreateTransactionWithOutput(destination.ScriptPubKey, amount);
		var signedTxId = signedTx.GetHash();

		// Add payment and move to uncertain state
		paymentBatch.AddPayment(destination, amount);
		var payments = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(payments, uint256.One);
		paymentBatch.MovePaymentsToSigned(signedTxId, []);

		// Create a DIFFERENT transaction (different txId)
		var differentTx = CreateTransactionWithOutput(destination.ScriptPubKey, Money.Coins(0.2m));
		var smartTx = new SmartTransaction(differentTx, Height.Mempool);

		// Try to resolve - should NOT succeed because txId doesn't match
		var resolved = paymentBatch.TryResolvePaymentsWithTransaction(smartTx);

		Assert.False(resolved);
		Assert.True(paymentBatch.AreThereUncertainPayments);
	}

	/// <summary>
	/// A signed payment whose round failed goes back to pending, but it can only be made again
	/// in a transaction that also spends one of the inputs of the transaction it was signed in.
	/// </summary>
	[Fact]
	public void FailedSignedPaymentIsOnlyRetriedInConflictingTransaction()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 3);
		var inputs = GetInputs(tx);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx.GetHash(), inputs);

		paymentBatch.MoveSignedPaymentsToPending();

		Assert.True(paymentBatch.AreTherePendingPayments);
		Assert.False(paymentBatch.AreThereUncertainPayments);
		var failedAttempt = Assert.Single(paymentBatch.GetPayments().Single().SignedAttempts);
		Assert.Equal(tx.GetHash(), failedAttempt.TransactionId);

		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: []).PaymentCount);
		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: [BitcoinFactory.CreateOutPoint()]).PaymentCount);
		Assert.Equal(1, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: [BitcoinFactory.CreateOutPoint(), inputs[1]]).PaymentCount);
	}

	/// <summary>
	/// After two failed attempts the retry must conflict with both, otherwise the first one
	/// and the retry could both confirm.
	/// </summary>
	[Fact]
	public void RetryMustConflictWithEveryFailedAttempt()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var tx1 = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 2);
		var tx2 = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 2);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx1.GetHash(), GetInputs(tx1));
		paymentBatch.MoveSignedPaymentsToPending();

		var retry = paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: [GetInputs(tx1)[0]]);
		paymentBatch.MovePaymentsToInProgress(retry.Payments, uint256.One);
		paymentBatch.MovePaymentsToSigned(tx2.GetHash(), GetInputs(tx2));
		paymentBatch.MoveSignedPaymentsToPending();

		Assert.Equal(2, paymentBatch.GetPayments().Single().SignedAttempts.Length);
		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: GetInputs(tx2)).PaymentCount);
		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: GetInputs(tx1)).PaymentCount);
		Assert.Equal(1, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: [GetInputs(tx1)[1], GetInputs(tx2)[0]]).PaymentCount);
	}

	/// <summary>
	/// A failed attempt can never confirm once a confirmed transaction spent one of our coins it spends, so from then on
	/// the retry doesn't need to spend any of its inputs. An unconfirmed spend is not enough, and a reorg brings it back.
	/// </summary>
	[Fact]
	public void FailedAttemptNoLongerCountsOnceItCanNoLongerConfirm()
	{
		var coins = new CoinsRegistry();
		var paymentBatch = new PaymentBatch(coins);
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var ourCoin = CreateOurCoin(coins);
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 1);
		tx.Inputs.Add(ourCoin.Outpoint);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx.GetHash(), GetInputs(tx));
		paymentBatch.MoveSignedPaymentsToPending();

		var spenderTx = Transaction.Create(Network.Main);
		spenderTx.Inputs.Add(ourCoin.Outpoint);
		spenderTx.Outputs.Add(new TxOut(Money.Coins(0.5m), GetNewSegwitAddress().ScriptPubKey));
		var spender = new SmartTransaction(spenderTx, Height.Mempool);
		coins.Spend(ourCoin, spender);

		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: []).PaymentCount);

		spender.TryUpdate(new SmartTransaction(spenderTx, new Height.ChainHeight(100)));

		Assert.Equal(1, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: []).PaymentCount);

		spender.SetUnconfirmed();

		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: []).PaymentCount);
	}

	/// <summary>
	/// A failed attempt that confirmed made the payment, it must not free the payment for another one.
	/// </summary>
	[Fact]
	public void PaymentIsNotRetriedWhenItsFailedAttemptConfirmed()
	{
		var coins = new CoinsRegistry();
		var paymentBatch = new PaymentBatch(coins);
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var ourCoin = CreateOurCoin(coins);
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 1);
		tx.Inputs.Add(ourCoin.Outpoint);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx.GetHash(), GetInputs(tx));
		paymentBatch.MoveSignedPaymentsToPending();

		coins.Spend(ourCoin, new SmartTransaction(tx, new Height.ChainHeight(100)));

		Assert.Equal(0, paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: [BitcoinFactory.CreateOutPoint()]).PaymentCount);
	}

	/// <summary>
	/// The manager moves the in-progress payments back after every coinjoin. A signed payment is only moved back
	/// by the tracker, when its round didn't broadcast the transaction.
	/// </summary>
	[Fact]
	public void OnlyInProgressPaymentsAreMovedBackToPending()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var signedDestination = GetNewSegwitAddress();
		var inProgressDestination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var tx = CreateTransactionWithOutput(signedDestination.ScriptPubKey, amount, inputCount: 2);

		paymentBatch.AddPayment(signedDestination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx.GetHash(), GetInputs(tx));
		paymentBatch.AddPayment(inProgressDestination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().Where(p => p.State is PendingPayment).ToArray(), uint256.One);

		paymentBatch.MovePaymentsToPending();

		Assert.IsType<SignedUnknownPayment>(paymentBatch.GetPayments().Single(p => p.Destination.ScriptPubKey == signedDestination.ScriptPubKey).State);
		Assert.IsType<PendingPayment>(paymentBatch.GetPayments().Single(p => p.Destination.ScriptPubKey == inProgressDestination.ScriptPubKey).State);

		paymentBatch.MoveSignedPaymentsToPending();

		Assert.All(paymentBatch.GetPayments(), p => Assert.IsType<PendingPayment>(p.State));
	}

	/// <summary>
	/// A failed attempt that is broadcast after all makes the payment. It must be finished, even when it is pending again.
	/// </summary>
	[Fact]
	public void PendingPaymentIsFinishedWhenFailedAttemptIsSeen()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 2);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx.GetHash(), GetInputs(tx));
		paymentBatch.MoveSignedPaymentsToPending();

		Assert.True(paymentBatch.TryResolvePaymentsWithTransaction(new SmartTransaction(tx, Height.Mempool)));

		var finished = Assert.IsType<FinishedPayment>(paymentBatch.GetPayments().Single().State);
		Assert.Equal(tx.GetHash(), finished.TransactionId);
		Assert.False(paymentBatch.AreTherePendingPayments);
	}

	[Fact]
	public void SignedPaymentsOfBroadcastTransactionAreFinished()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var destination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);
		var tx = CreateTransactionWithOutput(destination.ScriptPubKey, amount, inputCount: 2);

		paymentBatch.AddPayment(destination, amount);
		paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPayments().ToArray(), uint256.One);
		paymentBatch.MovePaymentsToSigned(tx.GetHash(), GetInputs(tx));

		paymentBatch.MovePaymentsToFinished(uint256.One);
		Assert.IsType<SignedUnknownPayment>(paymentBatch.GetPayments().Single().State);

		paymentBatch.MovePaymentsToFinished(tx.GetHash());
		paymentBatch.MovePaymentsToPending();
		Assert.IsType<FinishedPayment>(paymentBatch.GetPayments().Single().State);
	}

	/// <summary>
	/// Verifies that all payments from the same coinjoin are resolved together
	/// when the matching transaction is found.
	/// </summary>
	[Fact]
	public void AllPaymentsFromSameCoinjoinResolvedTogether()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
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
		paymentBatch.MovePaymentsToSigned(signedTxId, []);

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
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var uncertainDestination = GetNewSegwitAddress();
		var pendingDestination = GetNewSegwitAddress();
		var amount = Money.Coins(0.1m);

		// Add first payment and move to uncertain
		paymentBatch.AddPayment(uncertainDestination, amount);
		var firstPayment = paymentBatch.GetPayments().ToArray();
		paymentBatch.MovePaymentsToInProgress(firstPayment, uint256.One);
		var signedTxId = CreateTransactionWithOutput(uncertainDestination.ScriptPubKey, amount).GetHash();
		paymentBatch.MovePaymentsToSigned(signedTxId, []);

		// Add a new pending payment
		paymentBatch.AddPayment(pendingDestination, amount);

		Assert.True(paymentBatch.AreTherePendingPayments);
		Assert.True(paymentBatch.AreThereUncertainPayments);

		// GetBestPaymentSet should return the pending payment, not the uncertain one
		var availableMoney = Money.Coins(1m);
		var availableVsize = 1000;
		var bestPaymentSet = paymentBatch.GetBestPaymentSet(availableMoney, availableVsize, roundParameters, registeredInputs: []);

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
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
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
		paymentBatch.MovePaymentsToSigned(signedTxId, []);

		// Step 4: Round ending is UNKNOWN - payments stay in signed state
		// They are NOT moved back to pending to avoid double payments
		Assert.False(paymentBatch.AreTherePendingPayments, "Payment should NOT be pending after signing");
		Assert.True(paymentBatch.AreThereUncertainPayments, "Payment should be in signed state awaiting resolution");

		// This is the key check - GetBestPaymentSet should return empty
		var secondRoundPaymentSet = paymentBatch.GetBestPaymentSet(Money.Coins(1m), 1000, roundParameters, registeredInputs: []);
		Assert.Equal(0, secondRoundPaymentSet.PaymentCount);

		// The double payment bug would have failed here because the payment
		// would have been included in the second coinjoin
	}

	private static BitcoinAddress GetNewSegwitAddress()
	{
		using Key key = new();
		return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
	}

	private static Transaction CreateTransactionWithOutput(Script scriptPubKey, Money amount, int inputCount = 0)
	{
		var tx = Transaction.Create(Network.Main);
		for (var i = 0; i < inputCount; i++)
		{
			tx.Inputs.Add(BitcoinFactory.CreateOutPoint());
		}
		tx.Outputs.Add(new TxOut(amount, scriptPubKey));
		return tx;
	}

	private static ImmutableArray<OutPoint> GetInputs(Transaction tx) =>
		tx.Inputs.Select(i => i.PrevOut).ToImmutableArray();

	private static SmartCoin CreateOurCoin(CoinsRegistry coins)
	{
		var keyManager = KeyManager.CreateNew(out _, "", Network.Main);
		var coin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(keyManager), Money.Coins(0.5m));
		coins.TryAdd(coin);
		return coin;
	}
}