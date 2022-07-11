using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class ForeignVirtualOutput
{
	public ForeignVirtualOutput(Script script, Money amount, ISet<OutPoint> outPoints)
	{
		Script = script;
		Amount = amount;
		OutPoints = outPoints;
	}

	public Script Script { get; }
	public Money Amount { get; }
	public ISet<OutPoint> OutPoints { get; }
}
