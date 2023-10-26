using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AddressesModel : IDisposable
{
	private readonly CompositeDisposable _disposable = new();
	private readonly KeyManager _walletKeyManager;

	public AddressesModel(IObservable<Unit> transactionsTransactionProcessed, KeyManager walletKeyManager)
	{
		_walletKeyManager = walletKeyManager;
		var sourceCache = new SourceCache<IAddress, string>(address => address.Text)
			.DisposeWith(_disposable);
		transactionsTransactionProcessed
			.Do(_ => sourceCache.EditDiff(GetAddresses(), (one, another) => string.Equals(one.Text, another.Text, StringComparison.Ordinal)))
			.Subscribe()
			.DisposeWith(_disposable);

		var changes = sourceCache.Connect();

		Addresses = changes;
		var unusedAddresses = changes.AutoRefresh(x => x.IsUsed).Filter(x => !x.IsUsed);
		UnusedAddresses = unusedAddresses;
		var unusedCache = unusedAddresses.AsObservableCache().DisposeWith(_disposable);
		HasUnusedAddresses = unusedCache.CountChanged.Select(i => i > 1);
	}

	public IObservable<IChangeSet<IAddress, string>> UnusedAddresses { get; }

	public IObservable<bool> HasUnusedAddresses { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	public void Dispose() => _disposable.Dispose();

	private IEnumerable<IAddress> GetAddresses() => _walletKeyManager
		.GetKeys()
		.Reverse()
		.Select(x => new Address(_walletKeyManager, x));
}
