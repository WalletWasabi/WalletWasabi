using Avalonia.Media.Imaging;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices;
using ZXing.QrCode;
using SkiaSharp;
using System.Threading;
using System.Threading.Tasks;
using QRackers.Capturing;
using QRackers.Capturing.Utilities;

namespace WalletWasabi.Fluent.Models.UI;

public class QrCodeReader : IQrCodeReader
{
	private readonly QRCodeReader _decoder = new();

	public bool IsPlatformSupported =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public IObservable<(string decoded, Bitmap bitmap)> Read()
	{
		return Observable.Create(
			async (IObserver<(string, Bitmap)> result, CancellationToken ct) =>
			{
				var devices = new CaptureDevices();
				var device = devices
					.EnumerateDescriptors()
					.SelectMany(static d => d.Characteristics, static (d, c) => new { d, c })
					.FirstOrDefault() ?? throw new InvalidOperationException("Could not find a device.");

				await using var capture = await device.d
					.OpenAsync(
						device.c,
						ct: ct,
						pixelBufferArrived: scope =>
						{
							var decoded = Decode(scope);
							var bitmap = new Bitmap(scope.Buffer.ReferImage().AsStream());

							result.OnNext((decoded, bitmap));
						})
					.ConfigureAwait(false);

				var tcs = new TaskCompletionSource<object?>();

				ct.Register(() => tcs.TrySetResult(default));

				await capture.StartAsync(ct).ConfigureAwait(false);
				await tcs.Task.ConfigureAwait(false);
			});
	}

	private string Decode(PixelBufferScope scope)
	{
		using var bitmap = SKBitmap.Decode(scope.Buffer.ReferImage());
		return _decoder.DecodeBitmap(bitmap)?.Text ?? "";
	}
}
