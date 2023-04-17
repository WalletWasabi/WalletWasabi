using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class Output
{
	public Output(Money amount, BitcoinAddress destination, bool isSpent, IEnumerable<Feature> features)
	{
		Amount = amount;
		Destination = destination;
		IsSpent = isSpent;
		Features = features;
	}

	public Money Amount { get; }
	public BitcoinAddress Destination { get; }
	public bool IsSpent { get; }
	public IEnumerable<Feature> Features { get; }
}
