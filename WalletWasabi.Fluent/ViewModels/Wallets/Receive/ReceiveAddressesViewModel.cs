using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Addresses Awaiting Payment")]
public partial class ReceiveAddressesViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private IEnumerable<AddressesModel> _addresses;
	[AutoNotify] private FlatTreeDataGridSource<AddressViewModel> _source = new(Enumerable.Empty<AddressViewModel>());

	private ReceiveAddressesViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		EnableBack = true;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		LoadCommand = wallet.OnDemandAddresses.LoadCommand;
		LoadCommand.IsExecuting.BindTo(this, x => x.IsBusy);

		wallet.OnDemandAddresses.Addresses
			.ToObservableChangeSet()
			.Transform(address => new AddressViewModel(UiContext, async address => { }, address => { }, address))
			.Bind(out var viewModels)
			.Subscribe();

		Source = ReceiveAddressesDataGridSource.Create(viewModels);
	}

	public ReactiveCommand<Unit, IAddress> LoadCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		//_wallet
		//	.AddressesModel.UnusedAddressesCache
		//	.Connect()
		//	.Transform(CreateAddressViewModel)
		//	.Bind(out var addresses)
		//	.Subscribe()
		//	.DisposeWith(disposables);

		//var source = ReceiveAddressesDataGridSource.Create(addresses);

		//Source = source;
		//Source.RowSelection!.SingleSelect = true;
		//Source.DisposeWith(disposables);

		//base.OnNavigatedTo(isInHistory, disposables);
	}

	//private AddressViewModel CreateAddressViewModel(IAddress address)
	//{
	//	//return new AddressViewModel(UiContext, OnEditAddressAsync, address1 => OnShowAddressAsync(address1), address);
	//}

	private void OnShowAddressAsync(IAddress a)
	{
		//UiContext.Navigate().To().ReceiveAddress(_wallet, a, Services.UiConfig.Autocopy);
	}

	private async Task OnEditAddressAsync(IAddress address)
	{
		//var result = await Navigate().To().AddressLabelEdit(_wallet, address).GetResultAsync();
		//if (result is { } labels)
		//{
		//	address.SetLabels(labels);
		//}
	}
}
