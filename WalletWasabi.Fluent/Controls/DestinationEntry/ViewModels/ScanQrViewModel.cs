using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class ScanQrViewModel
{
	public bool IsVisible { get; }

	public ScanQrViewModel(Network network, bool isVisible)
	{
		IsVisible = isVisible;
		ScanQrCommand = ReactiveCommand.CreateFromObservable(() => GetAddressFromQrCode(network));
	}

	public ReactiveCommand<Unit, string> ScanQrCommand { get; set; }

	private IObservable<string> GetAddressFromQrCode(Network network)
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
