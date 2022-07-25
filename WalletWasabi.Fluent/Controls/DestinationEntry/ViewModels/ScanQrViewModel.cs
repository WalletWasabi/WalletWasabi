using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class ScanQrViewModel
{
	public ScanQrViewModel(Network network)
	{
		ScanQrCommand = ReactiveCommand.CreateFromObservable(GetAddressFromQrCode);
	}

	public ReactiveCommand<Unit, string> ScanQrCommand { get; set; }

	private static IObservable<string> GetAddressFromQrCode()
	{
		var dialog = Observable.FromAsync(
			async () =>
			{
				ShowQrCameraDialogViewModel dialog = new(Network.TestNet);
				return await RoutableViewModel.NavigateDialogAsync(
					dialog,
					NavigationTarget.CompactDialogScreen);
			});

		return dialog
			.Select(x => x.Result)
			.WhereNotNull();
	}
}
