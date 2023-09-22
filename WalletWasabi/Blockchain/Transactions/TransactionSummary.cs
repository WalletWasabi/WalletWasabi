using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount)
	{
		Transaction = tx;
		Amount = amount;

		FirstSeen = tx.FirstSeen;
		Labels = tx.Labels;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	public DateTimeOffset FirstSeen { get; set; }
	public LabelsArray Labels { get; set; }
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsCancellation => Transaction.IsCancellation;
	public bool IsSpeedup => Transaction.IsSpeedup;
	public bool IsCPFP => Transaction.IsCPFP;
	public bool IsCPFPd => Transaction.IsCPFPd;

	public Money? Fee
	{
		get
		{
			if (Transaction.TryGetFee(out var fee))
			{
				return fee;
			}

			return null;
		}
	}

	public uint256 GetHash() => Transaction.GetHash();
	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();
}
