using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class ForeignVirtualOutput
{
	/// <param name="id">Unique public key identifier or if we can't have that, then the scriptpubkey byte array.</param>
	public ForeignVirtualOutput(byte[] id, Money amount, ISet<OutPoint> outPoints)
	{
		Id = id;
		Amount = amount;
		OutPoints = outPoints;
	}

	/// <summary>Unique public key identifier or if we can't have that, then the scriptpubkey byte array. </summary>
	public byte[] Id { get; }

	public Money Amount { get; }
	public ISet<OutPoint> OutPoints { get; }
}
