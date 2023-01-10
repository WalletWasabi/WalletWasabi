using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public interface ISmartCoin
{
	Money Amount { get; }

	/// <summary>Script type if available, throw an exception if it is not available.</summary>
	/// <remarks>Note there might be a new script type that even NBitcoin does not support.</remarks>
	ScriptType ScriptType { get; }

	Script ScriptPubKey { get; }

	double AnonymitySet { get; }

	uint256 TransactionId { get; }

	uint Index { get; }
}
