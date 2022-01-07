using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Models;

public class TransactionAuthorizationInfo
{
	public TransactionAuthorizationInfo(BuildTransactionResult buildTransactionResult)
	{
		Psbt = buildTransactionResult.Psbt;
		Transaction = buildTransactionResult.Transaction;
	}

	public SmartTransaction Transaction { get; set; }

	public PSBT Psbt { get; }
}
