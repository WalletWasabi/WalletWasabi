using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class KnownOutput : IOutput
{
	private readonly SmartCoin _coin;
	private readonly Network _network;

	public KnownOutput(SmartCoin coin, Network network)
	{
		_coin = coin;
		_network = network;
	}

	public Money Amount => _coin.Amount;
	public BitcoinAddress Destination => _coin.ScriptPubKey.GetDestinationAddress(_network)!;
	public double Anonscore => _coin.HdPubKey.AnonymitySet;
	public uint Index => _coin.Index;
}
