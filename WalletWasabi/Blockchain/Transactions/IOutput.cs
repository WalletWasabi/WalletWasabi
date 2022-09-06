using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public interface IOutput
{
	Money Amount { get; }
	public BitcoinAddress Destination { get; }
}
