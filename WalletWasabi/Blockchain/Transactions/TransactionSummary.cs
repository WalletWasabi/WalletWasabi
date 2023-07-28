using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, IEnumerable<IInput> inputs, IEnumerable<Output> outputs, IEnumerable<BitcoinAddress> destinationAddresses)
	{
		Transaction = tx;
		Amount = amount;
		Inputs = inputs;
		Outputs = outputs;
		DestinationAddresses = destinationAddresses;

		DateTime = tx.FirstSeen;
		Labels = tx.Labels;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	public IEnumerable<IInput> Inputs { get; }
	public IEnumerable<Output> Outputs { get; }
	public IEnumerable<BitcoinAddress> DestinationAddresses { get; }
	public DateTimeOffset DateTime { get; set; }
	public LabelsArray Labels { get; set; }
	public uint256 TransactionId => Transaction.GetHash();
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsOwnCoinjoin => Transaction.IsOwnCoinjoin();
	public int VirtualSize => Transaction.Transaction.GetVirtualSize();

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
