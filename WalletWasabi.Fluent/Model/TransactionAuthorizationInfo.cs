using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Model
{
	public class TransactionAuthorizationInfo
	{
		public TransactionAuthorizationInfo(BuildTransactionResult buildTransactionResult)
		{
			BuildTransactionResult = buildTransactionResult;
			Transaction = buildTransactionResult.Transaction;
		}

		public BuildTransactionResult BuildTransactionResult { get; }

		public SmartTransaction Transaction { get; set; }
	}
}
