using WalletWasabi.Bridge;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using Wallet = WalletWasabi.Wallets.Wallet;

namespace WalletWasabi.Fluent.ViewModels;

public static class ViewModelLocator
{
	public static RoutableViewModel CreateReceiveAddressHostViewModel(Wallet wallet, IAddress newAddress)
	{
		var ra = new ReceiveAddressViewModel(newAddress, new QrGenerator());
		var hw = new HardwareWalletViewModel(newAddress, new HardwareInterfaceClient());
		var receiveAddressHostViewModel = new ReceiveAddressHostViewModel(ra, hw, wallet.KeyManager.IsHardwareWallet);
		return receiveAddressHostViewModel;
	}
}
