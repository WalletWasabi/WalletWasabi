using System.Reactive.Linq;
using System.Windows.Input;
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
		IsQrButtonVisible = WebcamQrReader.IsOsPlatformSupported;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		PaymentViewModel = Factory.Create(new BtcOnlyAddressParser(network), _ => true);
		QrCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			ShowQrCameraDialogViewModel dialog = new(network);
			var result = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);
			if (!string.IsNullOrWhiteSpace(result.Result))
			{
				PaymentViewModel.MutableAddressHost.Text = result.Result;
			}
		});

		var nextCommandCanExecute = PaymentViewModel.MutableAddressHost.ParsedAddress.Select(x => x is not null);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, BitcoinAddress.Create(PaymentViewModel.Address, network)), nextCommandCanExecute);
	}

	public PaymentViewModel PaymentViewModel { get; }

	public bool IsQrButtonVisible { get; }

	public ICommand QrCommand { get; }
}
