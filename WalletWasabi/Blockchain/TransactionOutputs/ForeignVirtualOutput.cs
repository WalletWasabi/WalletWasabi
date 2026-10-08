namespace WalletWasabi.Blockchain.TransactionOutputs;

public class ForeignVirtualOutput
{
	public ForeignVirtualOutput(Money amount, ISet<OutPoint> outPoints)
	{
		Amount = amount;
		OutPoints = outPoints;
	}

	public Money Amount { get; }
	public ISet<OutPoint> OutPoints { get; }
}
