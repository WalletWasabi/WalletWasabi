using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>Adapts the existing, wallet-generated receive address to native request state.</summary>
public sealed class MobileReceiveRequestViewModel : MobilePaymentRequestState
{
	public MobileReceiveRequestViewModel(ReceiveAddressViewModel source)
		: base(source.Address, source.UiContext.QrCodeGenerator.Generate,
			source.UiContext.Clipboard.SetTextAsync, RxApp.MainThreadScheduler)
	{
		Source = source;
	}

	public ReceiveAddressViewModel Source { get; }
}
