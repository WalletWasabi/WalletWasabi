using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using DynamicData;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
#pragma warning disable CA2000

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AddressesModel : IDisposable
{
	private readonly CompositeDisposable _disposable = new();
	private readonly KeyManager _keyManager;

	public AddressesModel(IObservable<Unit> transactionProcessed, KeyManager keyManager)
	{
		_keyManager = keyManager;
		
		var addressFetcher = new SignaledFetcher<IAddress, string>(transactionProcessed, address => address.Text, GetAddresses).DisposeWith(_disposable);
		Cache = addressFetcher.Cache;
		UnusedAddressesCache = addressFetcher.Cache.Connect().AutoRefresh(x => x.IsUsed).Filter(x => !x.IsUsed).AsObservableCache().DisposeWith(_disposable);
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
