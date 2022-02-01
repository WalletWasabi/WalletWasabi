using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using OpenCvSharp;
using Avalonia;
using Avalonia.Media;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;
using NBitcoin;
using Nito.AsyncEx;
using Avalonia.Platform;
using WalletWasabi.Fluent.Models.Windows;
using Avalonia.Controls;
using Microsoft.Extensions.Hosting;
using System.Threading;
using ZXing.QrCode;
using WalletWasabi.Bases;

namespace WalletWasabi.Fluent.Models;

public class WebcamQrReader : PeriodicRunner
{
	private const byte DefaultCameraId = 0;
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
		QRCodeReader? decoder = null;

		try
		{
			decoder = new QRCodeReader();
			string[] devices = WindowsCapture.FindDevices();
			if (devices.Length == 0)
			{
				ErrorOccurred?.Invoke(this, new NotSupportedException("Could not open camera."));
			}

			WindowsCapture.VideoFormat[] formats = WindowsCapture.GetVideoFormat(DefaultCameraId);

			Camera = new WindowsCapture(DefaultCameraId, formats[0]);
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
		Camera?.Release();

		await base.StopAsync(cancellationToken);
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		if (Camera is { })
		{
			var bmp = Camera.GetBitmap();
			NewImageArrived?.Invoke(this, bmp);
			//decoder.DecodeBitmap(bmp);
		}
		return Task.CompletedTask;
	}
}
