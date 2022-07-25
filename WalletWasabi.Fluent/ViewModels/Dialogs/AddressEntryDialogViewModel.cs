using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
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

		NextCommand = ReactiveCommand
			.Create(() => Close(DialogResultKind.Normal, BitcoinAddress.Create(Address, _network)), BigController.PaymentViewModel.IsValid())
			.DisposeWith(disposables);
		
		base.OnNavigatedTo(isInHistory, disposables);
	}

	public BigController? BigController
	{
		get => _bigController;
		set
		{
			_bigController = value;
			this.RaisePropertyChanged(nameof(BigController));
		}
	}

	public string Address { get; set; }
}
