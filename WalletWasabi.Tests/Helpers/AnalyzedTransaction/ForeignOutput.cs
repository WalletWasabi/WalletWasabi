using NBitcoin;

namespace WalletWasabi.Tests.Helpers.AnalyzedTransaction;

public record ForeignOutput(Transaction Transaction, uint Index)
{
	public OutPoint ToOutPoint() => new(Transaction, Index);

	public static ForeignOutput Create(Money amount, Script scriptPubKey)
	{
		Transaction transaction = Transaction.Create(Network.Main);
		TxOut txOut = new(amount, scriptPubKey);
		transaction.Outputs.Add(txOut);
		return new ForeignOutput(transaction, 0);
	}
}
