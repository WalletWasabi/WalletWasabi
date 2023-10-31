using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.Models.Wallets;

public record CancellingTransaction(
	TransactionModel TargetTransaction,
	BuildTransactionResult CancelTransaction,
	Amount Fee);
