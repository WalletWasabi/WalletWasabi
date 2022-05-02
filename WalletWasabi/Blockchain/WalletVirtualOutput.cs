using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain;

public record WalletVirtualOutput
{
	public byte[] KeyIdentifier;
	public Money Amount;
	public HashSet<OutPoint> Outpoints;

	public WalletVirtualOutput(byte[] keyIdentifier, Money amount, HashSet<OutPoint> outpoints)
	{
		KeyIdentifier = keyIdentifier;
		Amount = amount;
		Outpoints = outpoints;
	}
}
