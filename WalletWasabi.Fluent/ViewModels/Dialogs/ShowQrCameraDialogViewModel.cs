using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Userfacing;
using WalletWasabi.Userfacing.Bip21;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
{
	private readonly Network _network;
	[AutoNotify] private Bitmap? _qrImage;
	[AutoNotify] private string _errorMessage = "";
	[AutoNotify] private string _qrContent = "";

	public ShowQrCameraDialogViewModel(UiContext context, Network network)
	{
		Title = Lang.Resources.ShowQrCameraDialogViewModel_Title;

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
					if (AddressStringParser.TryParse(result.decoded, _network, out Bip21UriParser.Result? parserResult, out string? errorMessage))
					{
						Close(DialogResultKind.Normal, result.decoded);
					}
					else
					{
						// Remember last error message and last QR content.
						if (errorMessage is not null)
						{
							if (!string.IsNullOrEmpty(result.decoded))
							{
								ErrorMessage = errorMessage;
							}
						}

						if (!string.IsNullOrEmpty(result.decoded))
						{
							QrContent = result.decoded;
						}

						// ... but show always the current bitmap.
						QrImage = result.bitmap;
					}
				},
				onError: error => Dispatcher.UIThread.Post(async () =>
					{
						Close();
						await ShowErrorAsync(
							Lang.Resources.ShowQrCameraDialogViewModel_Title,
							error.Message,
							Lang.Resources.ShowQrCameraDialogViewModel_Error_Generic_Caption,
							NavigationTarget.CompactDialogScreen);
					}))
			.DisposeWith(disposables);
	}
}
