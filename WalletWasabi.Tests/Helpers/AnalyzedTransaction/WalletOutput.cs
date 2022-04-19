using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Tests.Helpers.AnalyzedTransaction;

public record WalletOutput
{
	public SmartCoin SmartCoin;

	public WalletOutput(SmartCoin smartCoin)
	{
		SmartCoin = smartCoin;
	}

	public int Anonymity => SmartCoin.HdPubKey.AnonymitySet;

	public SmartCoin ToSmartCoin()
	{
		return SmartCoin;
	}

	public ForeignOutput ToForeignOutput()
	{
		return new ForeignOutput(SmartCoin.Transaction.Transaction, SmartCoin.Index);
	}

	public static WalletOutput Create(Money amount, HdPubKey hdPubKey)
	{
		ForeignOutput output = ForeignOutput.Create(amount, hdPubKey.P2wpkhScript);
		SmartTransaction smartTransaction = new SmartTransaction(output.Transaction, 0);
		SmartCoin smartCoin = new SmartCoin(smartTransaction, output.Index, hdPubKey);
		smartTransaction.AddWalletOutput(smartCoin);
		return new WalletOutput(smartCoin);
	}
}
