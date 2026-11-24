using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public record SpeedUpTransactionResult(SmartTransaction TransactionToSpeedUp, BuildTransactionResult BoostingTransaction, FeeRate OriginalFeeRate, FeeRate NewFeeRate)
{
	public Money GetFeeDifference()
	{
		var isCpfp = BoostingTransaction.Transaction.Transaction.Inputs.Any(x => x.PrevOut.Hash == TransactionToSpeedUp.GetHash());

		if (isCpfp)
		{
			return BoostingTransaction.Fee;
		}

		var originalFee = TransactionToSpeedUp.WalletInputs.Sum(x => x.Amount) - TransactionToSpeedUp.OutputValues.Sum();
		return BoostingTransaction.Fee - originalFee;
	}
}
