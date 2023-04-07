using Avalonia.Media.Imaging;
using FlashCap;
using FlashCap.Utilities;
using NBitcoin;
using ReactiveUI;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Userfacing;
using ZXing.SkiaSharp;
using ZXing.QrCode;
using SkiaSharp;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Camera")]
public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
{
	private readonly Network _network;
	private readonly QRCodeReader _decoder;
	[AutoNotify] private Bitmap? _qrImage;
	[AutoNotify] private string _errorMessage = "";
	[AutoNotify] private string _qrContent = "";

	public static bool IsPlatformSupported =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public ShowQrCameraDialogViewModel(Network network)
	{
		_network = network;
		_decoder = new();
		
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private CancellationTokenSource CancellationTokenSource { get; } = new();

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		new CaptureDevices()
			.EnumerateDescriptors()
			.ToObservable()
			.Skip(1)
			.SelectMany(static d => d.Characteristics, static (d, c) => new { d, c })
			.FirstOrDefaultAsync()
			.Select(static d => d ?? throw new InvalidOperationException("Could not find a device."))
			.Select(static d => d.d
				.AsObservableAsync(d.c)
				.ToObservable()
				.Select(static d => d
					.StartAsync()
					.ToObservable()
					.CombineLatest(d, static (_, scope) => scope))
				.Switch())
			.Switch()
			.Select(scope =>
			{
				var decoded = Decode(scope);
				var bitmap = AddressStringParser.TryParse(decoded, _network, out _)
					? null // No bitmap is required when the dialog is about to close
					: new Bitmap(scope.Buffer.ReferImage().AsStream());

				return (decoded, bitmap);
			})
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(
				onNext: result =>
				{
					// No bitmap means that it was successfully parsed.
					if (result.bitmap is null)
					{
						Close(DialogResultKind.Normal, result.decoded);
					}
					else
					{
						ErrorMessage = "No valid Bitcoin address found";
						QrContent = result.decoded;
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

	private string Decode(PixelBufferScope scope)
	{
		using var bitmap = SKBitmap.Decode(scope.Buffer.ReferImage());
		return _decoder.DecodeBitmap(bitmap)?.Text ?? string.Empty;
	}
}
