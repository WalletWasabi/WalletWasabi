using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Tests.Helpers.AnalyzedTransaction;

public record ForeignOutput
{
	public Transaction Transaction;
	public uint Index;

	public ForeignOutput(Transaction transaction, uint index)
	{
		Transaction = transaction;
		Index = index;
	}

	public OutPoint ToOutPoint()
	{
		return new OutPoint(Transaction, Index);
	}

	public static ForeignOutput Create(Money amount, Script scriptPubKey)
	{
		Transaction transaction = Transaction.Create(Network.Main);
		TxOut txOut = new(amount, scriptPubKey);
		transaction.Outputs.Add(txOut);
		return new ForeignOutput(transaction, 0);
	}

}
