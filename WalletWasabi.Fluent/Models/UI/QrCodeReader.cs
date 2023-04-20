using Avalonia.Media.Imaging;
using FlashCap;
using FlashCap.Utilities;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices;
using ZXing.QrCode;
using SkiaSharp;

namespace WalletWasabi.Fluent.Models.UI;

public class QrCodeReader : IQrCodeReader
{
	private readonly QRCodeReader _decoder = new();
	
	public bool IsPlatformSupported =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public IObservable<(string decoded, Bitmap bitmap)> Read()
	{
		return new CaptureDevices()
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
				var bitmap = new Bitmap(scope.Buffer.ReferImage().AsStream());

				return (decoded, bitmap);
			});
	}

	private string Decode(PixelBufferScope scope)
	{
		using var bitmap = SKBitmap.Decode(scope.Buffer.ReferImage());
		return _decoder.DecodeBitmap(bitmap)?.Text ?? "";
	}
}
