using NBitcoin;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using WalletWasabi.Fluent.Controls.Payment.ViewModels;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class PaymentViewModel : ReactiveValidationObject, IDisposable
{
	public PaymentViewModel(Network network, Func<decimal, bool> isAmountValid, IAddressParser parser)
	{
		var clipboard = new Clipboard();
		var newContentsChanged = clipboard.ContentChanged;
		AddressController = new AddressViewModel(parser);
		var contentChecker = new ContentChecker<string>(
			newContentsChanged,
			AddressController.TextChanged,
			s => parser.GetAddress(s).IsSuccess);
		ScanQrViewModel = new ScanQrViewModel(network, WebcamQrReader.IsOsPlatformSupported);
		PasteController = new PasteButtonViewModel(clipboard.ContentChanged,contentChecker.HasNewContent, ApplicationUtils.IsMainWindowActive);
		AmountController = new AmountViewModel(isAmountValid);
		AmountCurrencyDirectionController = new AmountCurrencyDirectionController(
			Services.UiConfig.SendAmountConversionReversed,
			b => Services.UiConfig.SendAmountConversionReversed = b);

		this.ValidationRule(x => x.AddressController, AddressController.IsValid(), "Address invalid");
		this.ValidationRule(x => x.AmountController, AmountController.IsValid(), "Amount invalid");
	}

	public AddressViewModel AddressController { get; }

	public ScanQrViewModel ScanQrViewModel { get; }

	public PasteButtonViewModel PasteController { get; }

	public AmountViewModel AmountController { get; }

	public AmountCurrencyDirectionController AmountCurrencyDirectionController { get; }

	public void Dispose()
	{
		PasteController.Dispose();
	}
}
