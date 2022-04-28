using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain;

public record WalletVirtualInput
{
	public HdPubKey HdPubKey;
	public HashSet<SmartCoin> SmartCoins;

	public WalletVirtualInput(HdPubKey hdPubKey, HashSet<SmartCoin> smartCoins)
	{
		SmartCoins = smartCoins;
		HdPubKey = hdPubKey;
	}
}
