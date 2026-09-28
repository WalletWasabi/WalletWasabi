using NBitcoin;
using System.Linq;

namespace WalletWasabi.Blockchain.Transactions;

public static class SmartTransactionExtensions
{
	public static uint GetConfirmations(this SmartTransaction transaction, uint blockchainTipHeight)
		=> transaction.Height switch
		{
			ChainHeight(var height) => blockchainTipHeight - height + 1,
			_ => 0
		};

	public static Money? GetFee(this SmartTransaction transaction)
	{
		if (transaction.TryGetFee(out Money? fee))
		{
			return fee;
		}

		return null;
	}

	/// <summary>Fee rate of the transaction together with its CPFP children and the parents it pays for, as they confirm together.</summary>
	/// <returns><c>null</c> when the fee of one of them is unknown.</returns>
	public static FeeRate? GetUnconfirmedChainFeeRate(this SmartTransaction tx)
	{
		var unconfirmedChain = new[] { tx }.Concat(tx.ChildrenPayForThisTx).Concat(tx.ParentsThisTxPaysFor).ToArray();

		Money totalFee = Money.Zero;
		foreach (var currentTx in unconfirmedChain)
		{
			// We must have all the inputs and know the size of the tx to estimate the feerate.
			if (!currentTx.TryGetFee(out var fee) || currentTx.IsSegwitWithoutWitness)
			{
				return null;
			}

			totalFee += fee;
		}

		return new FeeRate(totalFee, unconfirmedChain.Sum(x => x.Transaction.GetVirtualSize()));
	}

	public static bool CanBeSpeedUpUsingCpfp(this SmartTransaction tx) =>
		!tx.Confirmed && (tx.ForeignInputs.Count != 0 || tx.ForeignOutputs.Count != 0);

}
