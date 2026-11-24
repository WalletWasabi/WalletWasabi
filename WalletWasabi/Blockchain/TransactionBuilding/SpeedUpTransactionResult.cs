using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionBuilding;

/// <summary>
/// Represents the result of building a transaction to speed up another transaction.
/// </summary>
/// <param name="TransactionToSpeedUp">Transaction which is to be sped up.</param>
/// <param name="BoostingTransaction">Transaction to broadcast to accelerate <paramref name="TransactionToSpeedUp"/>.</param>
/// <param name="OriginalFeeRate">Effective fee rate of <paramref name="TransactionToSpeedUp"/>.</param>
/// <param name="NewFeeRate">Effective fee rate after applying <paramref name="BoostingTransaction"/>.</param>
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
