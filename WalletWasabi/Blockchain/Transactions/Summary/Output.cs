using NBitcoin;
namespace WalletWasabi.Blockchain.Transactions.Summary;

public class UnknownOutput : IOutput
{
	private readonly IndexedTxOut _txOut;
	private readonly Network _network;
	
	public UnknownOutput(IndexedTxOut txOut, Network network)
	{
		_txOut = txOut;
		_network = network;
	}

	public Money Amount => _txOut.TxOut.Value;
	public BitcoinAddress Destination => _txOut.TxOut.ScriptPubKey.GetDestinationAddress(_network)!;
}
