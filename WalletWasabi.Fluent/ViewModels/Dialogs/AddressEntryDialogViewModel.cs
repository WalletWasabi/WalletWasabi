using System.Reactive.Disposables;
using ReactiveUI.Validation.Extensions;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Address")]
public partial class AddressEntryDialogViewModel : DialogViewModelBase<BitcoinAddress?>
{
	private readonly Network _network;
	[AutoNotify] private PaymentViewModel? _controller;

	public AddressEntryDialogViewModel(Network network)
	{
		_network = network;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		Controller = new PaymentViewModel(_network, _ => true, new BtcOnlyAddressParser(_network))
			.DisposeWith(disposables);

		NextCommand = ReactiveCommand
			.Create(() => Close(DialogResultKind.Normal, BitcoinAddress.Create(Address, _network)), Controller.AddressController.IsValid())
			.DisposeWith(disposables);
		
		base.OnNavigatedTo(isInHistory, disposables);
	}

	public string Address { get; set; }
}
