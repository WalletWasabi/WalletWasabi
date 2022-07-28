using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Controls.Payment.ViewModels;

public class ScanQrViewModel
{
	public ScanQrViewModel(Network network, bool isVisible)
	{
		IsVisible = isVisible;
		ScanQrCommand = ReactiveCommand.CreateFromObservable(() => GetAddressFromQrCode(network));
	}

	public bool IsVisible { get; }

	public ReactiveCommand<Unit, string> ScanQrCommand { get; }

	private static IObservable<string> GetAddressFromQrCode(Network network)
	{
		var dialog = Observable.FromAsync(
			async () =>
			{
				ShowQrCameraDialogViewModel dialog = new(network);
				return await RoutableViewModel.NavigateDialogAsync(
					dialog,
					NavigationTarget.CompactDialogScreen);
			});

		return dialog
			.Select(x => x.Result)
			.WhereNotNull();
	}
}
