using System.Threading.Tasks;
using System.Runtime.InteropServices;
using WalletWasabi.Userfacing;
using NBitcoin;
using WalletWasabi.Fluent.Models.Windows;
using System.Threading;
using ZXing.QrCode;
using WalletWasabi.Bases;
using System.IO;
using ZXing;
using Avalonia.Media.Imaging;

namespace WalletWasabi.Fluent.Models;

public class WebcamQrReader : PeriodicRunner
{
	private const byte DefaultCameraId = 0;
	private QRCodeReader? Decoder { get; set; }
	private WindowsCapture? Camera { get; set; }

	public WebcamQrReader(Network network) : base(TimeSpan.FromMilliseconds(100))
	{
		Network = network;
	}

	public event EventHandler<Bitmap>? NewImageArrived;

	public event EventHandler<string>? CorrectAddressFound;

	public event EventHandler<string>? InvalidAddressFound;

	public event EventHandler<Exception>? ErrorOccurred;

	private Network Network { get; }
	public static bool IsOsPlatformSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new InvalidOperationException("OS not supported.");
		}

		try
		{
			string[] devices = WindowsCapture.FindDevices();
			if (devices.Length == 0)
			{
				ErrorOccurred?.Invoke(this, new NotSupportedException("Could not open camera."));
			}

			WindowsCapture.VideoFormat[] formats = WindowsCapture.GetVideoFormat(DefaultCameraId);

			Decoder = new();
			Camera = new(DefaultCameraId, formats[0]);
			Camera.Start();

			// Immediately after starting the USB camera,
			// GetBitmap() fails because image buffer is not prepared yet.
			_ = Camera.GetBitmap();
			await base.StartAsync(cancellationToken);
		}
		catch (Exception)
		{
			var ex = new InvalidOperationException("Could not read frames. Please make sure no other program uses your camera.");
			ErrorOccurred?.Invoke(this, ex);
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new InvalidOperationException("OS not supported.");
		}
		Camera?.Release();

		await base.StopAsync(cancellationToken);
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new InvalidOperationException("OS not supported.");
		}

		if (Camera is { })
		{
			Bitmap bmp = Camera.GetBitmap();
			using MemoryStream stream = new();
			bmp.Save(stream);
			using System.Drawing.Bitmap bitmap = new(stream);
			NewImageArrived?.Invoke(this, bmp);
			Result? result = Decoder?.DecodeBitmap(bitmap);
			if (result is { })
			{
				if (AddressStringParser.TryParse(result.Text, Network, out _))
				{
					CorrectAddressFound?.Invoke(this, result.Text);
				}
				else
				{
					InvalidAddressFound?.Invoke(this, result.Text);
				}
			}
		}
		return Task.CompletedTask;
	}
}
