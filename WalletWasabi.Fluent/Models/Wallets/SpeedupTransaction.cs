using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Models.Wallets;

public record SpeedupTransaction(
	SmartTransaction TargetTransaction,
	BuildTransactionResult BoostingTransaction,
	bool AreWePayingTheFee,
	Amount Fee
	);
