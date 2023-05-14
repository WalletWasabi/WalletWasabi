using Avalonia.Media.Imaging;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
					if (AddressStringParser.TryParse(result.decoded, _network, out string? errorMessage, out BitcoinUrlBuilder? uriBuilder))
					{
						Close(DialogResultKind.Normal, result.decoded);
					}
					else
					{
						// Remember last error message and last QR content.
						if (errorMessage is not null)
						{
							ErrorMessage = errorMessage;
						}

						if (!string.IsNullOrEmpty(result.decoded))
						{
							QrContent = result.decoded;
						}

						// ... but show always the current bitmap.
						QrImage = result.bitmap;
					}
				},
				onError: error =>
				{
					RxApp.MainThreadScheduler.Schedule(async () =>
					{
						await ShowErrorAsync(Title, error.Message, "Something went wrong");

						Close();
					});
				})
			.DisposeWith(disposables);
	}
}
