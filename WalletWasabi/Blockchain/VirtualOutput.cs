using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain;

public record VirtualOutput
{
	public Money Amount;
	public HashSet<OutPoint> Outpoints;

	public VirtualOutput(Money amount, HashSet<OutPoint> outpoints)
	{
		Amount = amount;
		Outpoints = outpoints;
	}
}
