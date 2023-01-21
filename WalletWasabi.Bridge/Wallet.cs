using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using RawWallet = WalletWasabi.Wallets.Wallet;

namespace WalletWasabi.Bridge;

public class Wallet : IWallet
{
	private readonly RawWallet _wallet;

	public Wallet(RawWallet wallet)
	{
		_wallet = wallet;
		var historyBuilder = new TransactionHistoryBuilder(_wallet);
		Transactions = Observable.FromEventPattern(_wallet, nameof(_wallet.WalletRelevantTransactionProcessed))
			.SelectMany(_ => historyBuilder.BuildHistorySummary())
			.ToObservableChangeSet(x => x.TransactionId)
			.Transform(ts => new Transaction(ts));
	}

	public string Name => _wallet.WalletName;
	public IObservable<IChangeSet<Transaction, uint256>> Transactions { get; }
	public IAddress CreateReceiveAddress(IEnumerable<string> destinationLabels)
	{
		if (_wallet.KeyManager.MasterFingerprint == null)
		{
			throw new InvalidOperationException("Master fingerprint should not be null");
		}

		var hdPubKey = _wallet.KeyManager.GetNextReceiveKey(new SmartLabel(destinationLabels));
		var address = new Address(hdPubKey, _wallet.Network, _wallet.KeyManager.MasterFingerprint.Value);
		return address;
	}
}
