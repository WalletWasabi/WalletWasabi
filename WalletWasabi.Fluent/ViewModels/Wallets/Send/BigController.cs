using NBitcoin;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class BigController : IDisposable
{
	public BigController(Network network, Func<decimal, bool> isAmountValid, IAddressParser parser)
	{
		var clipboard = new Clipboard();
		var newContentsChanged = clipboard.ContentChanged;
		IMutableAddressHost mutableAddressHost = new MutableAddressHost(parser);
		var contentChecker = new ContentChecker<string>(
			newContentsChanged,
			mutableAddressHost.TextChanged,
			s => parser.GetAddress(s) is not null);
		PaymentViewModel = new PaymentViewModel(
			newContentsChanged,
			mutableAddressHost,
			contentChecker,
			isAmountValid);
		ScanQrViewModel = new ScanQrViewModel(network, WebcamQrReader.IsOsPlatformSupported);
		PasteController = new PasteButtonViewModel(clipboard.ContentChanged, contentChecker);
	}

	public PaymentViewModel PaymentViewModel { get; }

	public ScanQrViewModel ScanQrViewModel { get; }

	public PasteButtonViewModel PasteController { get; }

	public void Dispose()
	{
		PaymentViewModel.Dispose();
		PasteController.Dispose();
	}
}
