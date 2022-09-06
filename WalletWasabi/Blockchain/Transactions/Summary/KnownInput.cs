using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class KnownInput : IInput
{
	private readonly SmartCoin _coin;
	private readonly Network _network;

	public KnownInput(SmartCoin coin, Network network)
	{
		_coin = coin;
		_network = network;
	}

	public virtual Money Amount => _coin.Amount;

	public BitcoinAddress Address => _coin.ScriptPubKey.GetDestinationAddress(_network)!;
	public double Anonscore => _coin.HdPubKey.AnonymitySet;
	public uint Index => _coin.Index;
	public OutPoint Outpoint => _coin.Outpoint;
}
