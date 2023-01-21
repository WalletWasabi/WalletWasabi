using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Receive Address")]
public partial class ReceiveAddressHostViewModel : RoutableViewModel
{
	public ReceiveAddressViewModel ReceiveAddress { get; }
	public HardwareWalletViewModel HardwareWallet { get; }
	public bool IsHardwareWallet { get; }

	public ReceiveAddressHostViewModel(ReceiveAddressViewModel receiveAddress, HardwareWalletViewModel hardwareWallet, bool isHardwareWallet)
	{
		ReceiveAddress = receiveAddress;
		HardwareWallet = hardwareWallet;
		IsHardwareWallet = isHardwareWallet;
		EnableBack = true;
		CancelCommand = ReactiveCommand.Create(() => Navigate().Clear(), hardwareWallet.IsBusy.Select(b => !b));
		NextCommand = CancelCommand;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		hardwareWallet.IsBusy.BindTo(this, x => x.IsBusy);
	}
}
