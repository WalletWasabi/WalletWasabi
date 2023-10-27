namespace WalletWasabi.Fluent.Models.Wallets;

public enum TransactionType
{
	Unknown,
	IncomingTransaction,
	OutgoingTransaction,
	SelfTransferTransaction,
	Coinjoin,
	CoinjoinGroup,
	Cancellation,
	CPFP
}
