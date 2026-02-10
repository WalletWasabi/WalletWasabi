using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Tests.Helpers.AnalyzedTransaction;

public record WalletOutput(SmartCoin Coin)
{
	public double Anonymity => Coin.HdPubKey.AnonymitySet;

	public SmartCoin ToSmartCoin() => Coin;

	public ForeignOutput ToForeignOutput()
	{
		return new ForeignOutput(Coin.Transaction.Transaction, Coin.Index);
	}

	public static WalletOutput Create(Money amount, HdPubKey hdPubKey)
	{
		ForeignOutput output = ForeignOutput.Create(amount, hdPubKey.P2wpkhScript);
		SmartTransaction smartTransaction = new(output.Transaction, Height.Unknown);
		SmartCoin smartCoin = new(smartTransaction, output.Index, hdPubKey);
		smartTransaction.TryAddWalletOutput(smartCoin);
		return new WalletOutput(smartCoin);
	}
}
