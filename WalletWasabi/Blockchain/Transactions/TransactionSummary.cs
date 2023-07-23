using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public DateTimeOffset DateTime { get; set; }
	public Height Height { get; init; }
	public Money Amount { get; set; }
	public LabelsArray Labels { get; set; }
	public uint256 TransactionId { get; init; }
	public uint256? BlockHash { get; init; }
	public int BlockIndex { get; init; }
	public bool IsOwnCoinjoin { get; init; }
	public IEnumerable<Output> Outputs { get; init; }
	public IEnumerable<IInput> Inputs { get; init; }
	public Money OutputAmount => Outputs.Sum(x => x.Amount);
	public Money? InputAmount => Inputs.Any(x => x.Amount == null) ? null : Inputs.Sum(x => x.Amount);
	public Money? Fee => InputAmount != null ? InputAmount - OutputAmount : null;
	public IEnumerable<BitcoinAddress> DestinationAddresses { get; init; }
	public int VirtualSize { get; init; }
	
	public FeeRate? FeeRate
	{
		get
		{
			if (Fee is null)
			{
				return null;
			}

			var rate = (double) Fee.Satoshi / VirtualSize;
			var money = new Money((decimal)rate, MoneyUnit.Satoshi);
			return new FeeRate(money * 100);
		}
	}
}
