using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>Owns only mobile presentation state; wallet navigation and hardware verification stay with Source.</summary>
public sealed class MobileReceiveRequestViewModel : MobileReceivePresentation
{
	public MobileReceiveRequestViewModel(ReceiveAddressViewModel source)
		: base(source.Address, source.UiContext.QrCodeGenerator.Generate,
			source.UiContext.Clipboard.SetTextAsync, RxApp.MainThreadScheduler)
	{
		Source = source;
	}

	public ReceiveAddressViewModel Source { get; }
}
