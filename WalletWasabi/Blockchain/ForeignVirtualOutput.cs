using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain;

public record ForeignVirtualOutput
{
	public byte[] KeyIdentifier;
	public Money Amount;
	public HashSet<OutPoint> Outpoints;

	public ForeignVirtualOutput(byte[] keyIdentifier, Money amount, HashSet<OutPoint> outpoints)
	{
		KeyIdentifier = keyIdentifier;
		Amount = amount;
		Outpoints = outpoints;
	}
}
