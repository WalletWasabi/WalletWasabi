using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.Models.Wallets;

public record CancellingTransaction(
	RegularTransactionModel TargetTransaction,
	BuildTransactionResult CancelTransaction,
	Amount Fee);
