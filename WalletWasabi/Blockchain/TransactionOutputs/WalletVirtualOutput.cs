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
	public WalletVirtualOutput(byte[] id, Money amount, ISet<OutPoint> outPoints)
	{
		Id = id;
		Amount = amount;
		OutPoints = outPoints;
	}

	public byte[] Id { get; }
	public Money Amount { get; }
	public ISet<OutPoint> OutPoints { get; }
}
