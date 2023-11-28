using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, IEnumerable<BitcoinAddress> destinationAddresses)
	{
		Transaction = tx;
		Amount = amount;
		DestinationAddresses = destinationAddresses;

		DateTime = tx.FirstSeen;
		Labels = tx.Labels;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	public IEnumerable<BitcoinAddress> DestinationAddresses { get; }
	public DateTimeOffset DateTime { get; set; }
	public LabelsArray Labels { get; set; }
	public uint256 TransactionId => Transaction.GetHash();
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsOwnCoinjoin => Transaction.IsOwnCoinjoin();
	public int VirtualSize => Transaction.Transaction.GetVirtualSize();
	public bool IsCancellation => Transaction.IsCancellation;
	public bool IsSpeedUp => Transaction.IsSpeedup;
	public bool IsCPFP => Transaction.IsCPFP;
	public bool IsCPFPd => Transaction.IsCPFPd;

	public FeeRate? FeeRate
	{
		get
		{
			if (Transaction.TryGetFeeRate(out var feeRate))
			{
				return feeRate;
			}

			return null;
		}
	}

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
}
