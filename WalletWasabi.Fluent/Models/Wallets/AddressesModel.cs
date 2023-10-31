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
	private readonly KeyManager _keyManager;

	public AddressesModel(IObservable<Unit> transactionProcessed, KeyManager keyManager)
	{
		_keyManager = keyManager;
		var sourceCache = new SourceCache<IAddress, string>(address => address.Text)
			.DisposeWith(_disposable);
		transactionProcessed
			.Do(_ => sourceCache.EditDiff(GetAddresses(), (one, another) => string.Equals(one.Text, another.Text, StringComparison.Ordinal)))
			.Subscribe()
			.DisposeWith(_disposable);

		var changes = sourceCache.Connect();

		Addresses = changes;
		var unusedAddresses = changes.AutoRefresh(x => x.IsUsed).Filter(x => !x.IsUsed);
		UnusedAddresses = unusedAddresses;
		var unusedCache = unusedAddresses.AsObservableCache().DisposeWith(_disposable);
		HasUnusedAddresses = unusedCache.CountChanged.Select(i => i > 0);
	}

	public IObservable<IChangeSet<IAddress, string>> UnusedAddresses { get; }

	public IObservable<bool> HasUnusedAddresses { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	public void Dispose() => _disposable.Dispose();

	private IEnumerable<IAddress> GetAddresses() => _keyManager
		.GetKeys()
		.Reverse()
		.Select(x => new Address(_keyManager, x));
}
