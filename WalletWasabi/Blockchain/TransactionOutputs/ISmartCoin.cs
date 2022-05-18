using NBitcoin;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public interface ISmartCoin
{
	Money Amount { get; }

	ScriptType ScriptType { get; }

	double AnonymitySet { get; }

	uint256 TransactionId { get; }

	uint Index { get; }
}
