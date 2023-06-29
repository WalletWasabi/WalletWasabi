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

	/// <returns>False if external, or the tx inputs are all external.</returns>
	/// <remarks>
	/// Context: https://github.com/zkSNACKs/WalletWasabi/issues/10567
	/// If you're a lazy dev implementing this interface, you may just return constant true, because it does not make too much of a difference.
	/// </remarks>
	bool IsSufficientlyDistancedFromExternalKeys { get; }
}
