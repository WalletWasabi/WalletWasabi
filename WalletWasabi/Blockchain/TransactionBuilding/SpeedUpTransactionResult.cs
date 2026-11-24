using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionBuilding;

/// <summary>
/// <paramref name="OriginalTransaction"/> was requested to speed up.
/// </summary>
public record SpeedUpTransactionResult(SmartTransaction OriginalTransaction, BuildTransactionResult BoostingTransaction, FeeRate OriginalFeeRate, FeeRate NewFeeRate)
{
	public Money GetFeeDifference()
	{
		var isCpfp = BoostingTransaction.Transaction.Transaction.Inputs.Any(x => x.PrevOut.Hash == OriginalTransaction.GetHash());

		if (isCpfp)
		{
			return BoostingTransaction.Fee;
		}

		var originalFee = OriginalTransaction.WalletInputs.Sum(x => x.Amount) - OriginalTransaction.OutputValues.Sum();
		return BoostingTransaction.Fee - originalFee;
	}
}
