using System.Linq;
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

	[AutoNotify] private FlatTreeDataGridSource<AddressViewModel> _source = new(Enumerable.Empty<AddressViewModel>());

	private ReceiveAddressesViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		EnableBack = true;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var loadCommand = _wallet.Addresses.LoadCommand;
		loadCommand.IsExecuting.BindTo(this, x => x.IsBusy);

		_wallet.Addresses.Unused
			.ToObservableChangeSet()
			.Transform(address => new AddressViewModel(UiContext, OnEditAddressAsync, OnShowAddressAsync, address))
			.Bind(out var viewModels)
			.Subscribe();

		Source = ReceiveAddressesDataGridSource.Create(viewModels).DisposeWith(disposables);
	}

	private void OnShowAddressAsync(IAddress a) => UiContext.Navigate().To().ReceiveAddress(_wallet, a, Services.UiConfig.Autocopy);

	private async Task OnEditAddressAsync(IAddress address)
	{
		var result = await Navigate().To().AddressLabelEdit(_wallet, address).GetResultAsync();
		if (result is { } labels)
		{
			address.SetLabels(labels);
		}
	}
}
