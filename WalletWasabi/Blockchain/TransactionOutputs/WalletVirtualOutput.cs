using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class WalletVirtualOutput
{
	public WalletVirtualOutput(HdPubKey hdPubKey, Money amount, ISet<OutPoint> outPoints)
	{
		HdPubKey = hdPubKey;
		Amount = amount;
		OutPoints = outPoints;
	}

	public HdPubKey HdPubKey { get; }
	public Money Amount { get; }
	public ISet<OutPoint> OutPoints { get; }
}
