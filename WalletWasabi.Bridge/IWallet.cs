using DynamicData;
using NBitcoin;

namespace WalletWasabi.Bridge;

public interface IWallet
{
	public string Name { get; }
	IObservable<IChangeSet<Transaction, uint256>> Transactions { get; }
	IAddress CreateReceiveAddress(IEnumerable<string> destinationLabels);
}
