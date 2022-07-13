using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class ForeignVirtualOutput
{
	/// <param name="keyId">Unique public key identifier or if we can't have that, then the scriptpubkey byte array.</param>
	public ForeignVirtualOutput(byte[] keyId, Money amount, ISet<OutPoint> outPoints)
	{
		KeyId = keyId;
		Amount = amount;
		OutPoints = outPoints;
	}

	/// <summary>Unique public key identifier or if we can't have that, then the scriptpubkey byte array. </summary>
	public byte[] KeyId { get; }

	public Money Amount { get; }
	public ISet<OutPoint> OutPoints { get; }
}
