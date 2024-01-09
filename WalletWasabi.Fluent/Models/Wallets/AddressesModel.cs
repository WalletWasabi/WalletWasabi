using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using DynamicData;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AddressesModel : IDisposable
{
	private readonly CompositeDisposable _disposable = new();
	private readonly KeyManager _keyManager;

	public AddressesModel(IObservable<Unit> addressesUpdated, KeyManager keyManager)
	{
		_keyManager = keyManager;

		Cache =
			addressesUpdated.Fetch(GetAddresses, address => address.Text)
								.DisposeWith(_disposable);

		UnusedAddressesCache =
			Cache.Connect()
				 .AutoRefresh(x => x.IsUsed)
				 .Filter(x => !x.IsUsed)
				 .AsObservableCache()
				 .DisposeWith(_disposable);

		HasUnusedAddresses = UnusedAddressesCache.NotEmpty();
	}

	public IObservableCache<IAddress, string> Cache { get; }

	public IObservableCache<IAddress, string> UnusedAddressesCache { get; }

	public IObservable<bool> HasUnusedAddresses { get; }

	public void Dispose() => _disposable.Dispose();

	private IEnumerable<IAddress> GetAddresses() => _keyManager
		.GetKeys()
		.Reverse()
		.Select(x => new Address(_keyManager, x));
}
