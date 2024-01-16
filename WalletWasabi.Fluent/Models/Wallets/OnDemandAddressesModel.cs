using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class OnDemandAddressesModel : ViewModelBase
{
	private readonly KeyManager _keyManager;

	public OnDemandAddressesModel(KeyManager keyManager)
	{
		_keyManager = keyManager;
		LoadCommand = ReactiveCommand.CreateFromObservable(() => GetAddresses().ToObservable(NewThreadScheduler.Default));

		var source = new SourceList<IAddress>();

		LoadCommand.IsExecuting
			.Where(executing => executing)
			.Do(_ => source.Clear())
			.Subscribe();

		LoadCommand
			.Do(l => source.Add(l))
			.Subscribe();

		source
			.Connect().Bind(out var items)
			.Subscribe();

		Addresses = items;
	}

	public ReadOnlyObservableCollection<IAddress> Addresses { get; set; }

	private IEnumerable<IAddress> GetAddresses()
	{
		return _keyManager.GetKeys()
			.Select(key => new Address(_keyManager, key))
			.Where(address => !address.IsUsed);
	}

	public ReactiveCommand<Unit, IAddress> LoadCommand { get; }
}
