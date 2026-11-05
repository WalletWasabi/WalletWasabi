using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AppLifetime]
public class AddressesModel
{
	private readonly ISubject<HdPubKey> _newAddressGenerated = new Subject<HdPubKey>();
	private readonly IServices _services;
	private readonly WalletId _walletId;
	private readonly KeyManager _keyManager;
	private readonly SourceList<HdPubKey> _source;

	public AddressesModel(IServices services, IWalletModel walletModel)
	{
		_services = services;
		_walletId = walletModel.Id;
		// Internal access to KeyManager through WalletModel for model-level operations
		_keyManager = ((WalletModel)walletModel).Wallet.KeyManager;

		_source = new SourceList<HdPubKey>();
		_source.AddRange(GetUnusedKeys());

		services.SubscribeToEvent<WalletRelevantTransactionProcessed>()
			.Do(_ => UpdateUnusedKeys())
			.Subscribe();

		_newAddressGenerated
			.Do(address => _source.Add(address))
			.Subscribe();

		_source.Connect()
			.Transform(key => (IAddress) new Address(_keyManager, key, Hide))
			.Bind(out var unusedAddresses)
			.Subscribe();

		Unused = unusedAddresses;
	}

	private IEnumerable<HdPubKey> GetUnusedKeys() => _services.GetUnusedKeys(_walletId);

	public IAddress NextReceiveAddress(IEnumerable<string> destinationLabels, ScriptPubKeyType scriptPubKeyType)
	{
		var labels = new LabelsArray(destinationLabels);
		var pubKey = _services.GetNextReceiveAddress(_walletId, labels, scriptPubKeyType);
		var nextReceiveAddress = new Address(_keyManager, pubKey, Hide);
		_newAddressGenerated.OnNext(pubKey);

		return nextReceiveAddress;
	}

	public ReadOnlyObservableCollection<IAddress> Unused { get; }

	public void Hide(Address address)
	{
		_services.SetKeyState(_walletId, KeyState.Locked, address.HdPubKey);
		_services.SaveKeyManager(_walletId);
		_source.Remove(address.HdPubKey);
	}

	private void UpdateUnusedKeys()
	{
		var itemsToRemove = _source.Items
			.Where(item => item.KeyState != KeyState.Clean)
			.ToList();

		foreach (var item in itemsToRemove)
		{
			_source.Remove(item);
		}
	}
}
