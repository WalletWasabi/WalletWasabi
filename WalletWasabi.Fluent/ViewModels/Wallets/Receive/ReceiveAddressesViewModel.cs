using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Addresses Awaiting Payment")]
public partial class ReceiveAddressesViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	private ReceiveAddressesViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		Source = CreateSource();

		Source.RowSelection!.SingleSelect = true;
		EnableBack = true;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public FlatTreeDataGridSource<AddressViewModel> Source { get; }

	private FlatTreeDataGridSource<AddressViewModel> CreateSource()
	{
		_wallet
			.UnusedAddresses()
			.Transform(CreateAddressViewModel)
			.Bind(out var addresses)
			.Subscribe();

		var source = ReceiveAddressesDataGridSource.Create(addresses);
		return source;
	}

	private AddressViewModel CreateAddressViewModel(IAddress address)
	{
		return new AddressViewModel(UiContext, NavigateToAddressEditAsync, NavigateToAddressAsync, address);
	}

	private async Task NavigateToAddressEditAsync(IAddress address)
	{
		var result = await NavigateDialogAsync(new AddressLabelEditViewModel(_wallet, address), NavigationTarget.CompactDialogScreen);
		if (result is { Kind: DialogResultKind.Normal, Result: not null })
		{
			address.SetLabels(result.Result);
		}
	}

	private async Task NavigateToAddressAsync(IAddress address)
	{
		UiContext.Navigate().To(new ReceiveAddressViewModel(UiContext, _wallet, address, Services.UiConfig.Autocopy));
	}
}
