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
	public Money OutputAmount => Outputs.Sum(x => x.Amount);
	public Money? InputAmount => Inputs.Any(x => x.Amount == null) ? null : Inputs.Sum(x => x.Amount);
	public Money? Fee => InputAmount != null ? InputAmount - OutputAmount : null;
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
			if (Fee is null)
			{
				return null;
			}

			return new FeeRate(Fee, VirtualSize);
		}
	}
}
