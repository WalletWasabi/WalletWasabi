using System.Diagnostics.CodeAnalysis;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public class EmptyTransactionStore : ITransactionStore
{
	public EmptyTransactionStore(Network network)
	{
		Network = network;
	}

	public Network Network { get; }

	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? sameStx)
	{
		sameStx = new SmartTransaction(Transaction.Create(Network), height: Unknown);
		return true;
	}
}
