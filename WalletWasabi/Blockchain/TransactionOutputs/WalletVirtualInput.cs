using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class WalletVirtualInput
{
	public WalletVirtualInput(HdPubKey hdPubKey, ISet<SmartCoin> coins)
	{
		HdPubKey = hdPubKey;
		Coins = coins;
	}

	public HdPubKey HdPubKey { get; }
	public ISet<SmartCoin> Coins { get; }
}
