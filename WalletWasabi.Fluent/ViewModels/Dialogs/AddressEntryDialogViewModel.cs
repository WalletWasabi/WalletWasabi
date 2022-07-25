using System.Reactive.Disposables;
using System.Reactive.Linq;
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
	private BigController _bigController;

	public AddressEntryDialogViewModel(Network network)
	{
		_network = network;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		BigController = new BigController(_network, _ => true, new BtcOnlyAddressParser(_network))
			.DisposeWith(disposables);

		var nextCommandCanExecute = PaymentViewModel.MutableAddressHost.ParsedAddress.Select(x => x is not null);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, BitcoinAddress.Create(PaymentViewModel.Address, _network)), nextCommandCanExecute)
			.DisposeWith(disposables);
		
		base.OnNavigatedTo(isInHistory, disposables);
	}

	public BigController? BigController
	{
		get => _bigController;
		set
		{
			_bigController = value;
			this.RaisePropertyChanged(nameof(ScanQrViewModel));
			this.RaisePropertyChanged(nameof(PaymentViewModel));
			this.RaisePropertyChanged(nameof(PasteController));
		}
	}
	public ScanQrViewModel? ScanQrViewModel => BigController?.ScanQrViewModel;

	public PaymentViewModel? PaymentViewModel => BigController?.PaymentViewModel;

	public PasteButtonViewModel? PasteController => BigController?.PasteController;
}
