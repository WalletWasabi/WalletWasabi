using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Address")]
public partial class AddressEntryDialogViewModel : DialogViewModelBase<BitcoinAddress?>
{
	public AddressEntryDialogViewModel(Network network)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		PaymentViewModel = Factory.Create(new BtcOnlyAddressParser(network), _ => true);
		ScanQrViewModel = new ScanQrViewModel(network, WebcamQrReader.IsOsPlatformSupported);

		var nextCommandCanExecute = PaymentViewModel.MutableAddressHost.ParsedAddress.Select(x => x is not null);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, BitcoinAddress.Create(PaymentViewModel.Address, network)), nextCommandCanExecute);
	}

	public ScanQrViewModel ScanQrViewModel { get; set; }

	public PaymentViewModel PaymentViewModel { get; }

}
