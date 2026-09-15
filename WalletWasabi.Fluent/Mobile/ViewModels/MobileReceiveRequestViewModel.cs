using Avalonia;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Services;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>Owns mobile presentation only; wallet navigation and hardware verification stay with Source.</summary>
public sealed class MobileReceiveRequestViewModel : MobileReceivePresentation
{
	public MobileReceiveRequestViewModel(ReceiveAddressViewModel source)
		: base(source.Address, source.UiContext.QrCodeGenerator.Generate,
			source.UiContext.Clipboard.SetTextAsync, RxApp.MainThreadScheduler, MobileSharing.GetService(Application.Current))
	{
		Source = source;
	}

	public ReceiveAddressViewModel Source { get; }
}
