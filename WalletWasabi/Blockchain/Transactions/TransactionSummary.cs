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
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	public IEnumerable<IInput> Inputs { get; }
	public IEnumerable<Output> Outputs { get; }
	public IEnumerable<BitcoinAddress> DestinationAddresses { get; }
	public DateTimeOffset DateTime { get; set; }
	public Height Height { get; init; }
	public LabelsArray Labels { get; set; }
	public uint256? BlockHash { get; init; }
	public int BlockIndex { get; init; }
	public bool IsOwnCoinjoin { get; init; }
	public Money OutputAmount => Outputs.Sum(x => x.Amount);
	public Money? InputAmount => Inputs.Any(x => x.Amount == null) ? null : Inputs.Sum(x => x.Amount);
	public Money? Fee => InputAmount != null ? InputAmount - OutputAmount : null;
	public int VirtualSize { get; init; }
	public uint256 TransactionId => Transaction.GetHash();

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
