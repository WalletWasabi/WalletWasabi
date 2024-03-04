using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Blockchain.Transactions;

/// <param name="PaymentIntent">Payment(s) to make.</param>
/// <param name="FeeRate"></param>
/// <param name="AllowUnconfirmed"><c>true</c> to allow unconfirmed coins, <c>false</c> otherwise.</param>
/// <param name="AllowDoubleSpend"><c>true</c> to allow double spending of coins, <c>false</c> otherwise.</param>
/// <param name="AllowedInputs">Set of coins that are allowed to be used in the created transaction, <c>null</c> to allow all inputs.</param>
/// <param name="TryToSign"><c>true</c> to return a transaction with signed inputs, <c>false</c> otherwise.</param>
/// <param name="OverrideFeeOverpaymentProtection"><c>true</c> to allow to build a transaction with more fees than outgoing amount, <c>false</c> otherwise.</param>
public record TransactionParameters(
	PaymentIntent PaymentIntent,
	FeeRate FeeRate,
	bool AllowUnconfirmed,
	bool AllowDoubleSpend,
	IEnumerable<OutPoint>? AllowedInputs,
	bool TryToSign,
	bool OverrideFeeOverpaymentProtection);
