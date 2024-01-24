using System.Diagnostics.CodeAnalysis;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public interface ITransactionStore
{
	bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? sameStx);
}
