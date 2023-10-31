using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;

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

		Cache = changes.AsObservableCache();

		UnusedAddressesCache = changes.AutoRefresh(x => x.IsUsed).Filter(x => !x.IsUsed).AsObservableCache();
		HasUnusedAddresses = UnusedAddressesCache.NotEmpty();
	}

	public IObservableCache<IAddress, string> UnusedAddressesCache { get; set; }

	public IObservableCache<IAddress, string> Cache { get; }

	public IObservable<bool> HasUnusedAddresses { get; }

	public void Dispose() => _disposable.Dispose();

	private IEnumerable<IAddress> GetAddresses() => _keyManager
		.GetKeys()
		.Reverse()
		.Select(x => new Address(_keyManager, x));
}
