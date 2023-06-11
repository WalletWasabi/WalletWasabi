using Avalonia.Media.Imaging;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Camera")]
public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
{
	private readonly Network _network;
	[AutoNotify] private Bitmap? _qrImage;
	[AutoNotify] private string _errorMessage = "";
	[AutoNotify] private string _qrContent = "";

	public ShowQrCameraDialogViewModel(UiContext context, Network network)
	{
		_network = network;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		UiContext = context;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		UiContext.QrCodeReader
			.Read()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(
				onNext: result =>
				{
					if (AddressStringParser.TryParse(result.decoded, _network, out _))
					{
						Close(DialogResultKind.Normal, result.decoded);
					}
					else
					{
						ErrorMessage = "No valid Bitcoin address found";
						QrContent = result.decoded ?? "";
						QrImage = result.bitmap;
					}
				},
				onError: error =>
				{
					Dispatcher.UIThread.Post(async () =>
					{
						Close();
						await ShowErrorAsync(Title, error.Message, "Something went wrong", NavigationTarget.CompactDialogScreen);
					});
				})
			.DisposeWith(disposables);
	}
}
