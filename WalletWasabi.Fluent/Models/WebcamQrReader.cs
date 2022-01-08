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

namespace WalletWasabi.Fluent.Models;

public class WebcamQrReader
{
	private const byte DefaultCameraId = 0;

	/// <summary>Whether user requested to stop webcamera to scan for QR codes.</summary>
	private volatile bool _requestEnd;

	public WebcamQrReader(Network network)
	{
		Network = network;
	}

	public event EventHandler<WriteableBitmap>? NewImageArrived;

	public event EventHandler<string>? CorrectAddressFound;

	public event EventHandler<string>? InvalidAddressFound;

	public event EventHandler<Exception>? ErrorOccured;

	private AsyncLock ScanningTaskLock { get; } = new();
	private Network Network { get; }
	private Task? ScanningTask { get; set; }
	public static bool IsOsPlatformSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public async Task StartScanningAsync()
	{
		using (await ScanningTaskLock.LockAsync().ConfigureAwait(false))
		{
			if (ScanningTask is { })
			{
				return;
			}
			_requestEnd = false;
			ScanningTask = Task.Run(() =>
			{
				VideoCapture? camera = null;
				try
				{
					if (!IsOsPlatformSupported)
					{
						throw new NotImplementedException("This operating system is not supported.");
					}
					camera = new();
					camera.SetExceptionMode(true);
						// Setting VideoCaptureAPI to DirectShow, to remove warning logs,
						// might need to be changed in the future for other operating systems
						if (!camera.Open(DefaultCameraId, VideoCaptureAPIs.DSHOW))
					{
						throw new InvalidOperationException("Could not open webcamera.");
					}
					KeepScanning(camera);
				}
				catch (OpenCVException ex)
				{
					Logger.LogError("Could not open camera. Reason: " + ex);
					ErrorOccured?.Invoke(this, new NotSupportedException("Could not open camera."));
				}
				catch (Exception ex)
				{
					Logger.LogError("QR scanning stopped. Reason:", ex);
					ErrorOccured?.Invoke(this, ex);
				}
				finally
				{
					camera?.Release();
					camera?.Dispose();
				}
			});
		}
	}

	public async Task StopScanningAsync()
	{
		using (await ScanningTaskLock.LockAsync().ConfigureAwait(false))
		{
			if (ScanningTask is { } task)
			{
				_requestEnd = true;
				await task;

				ScanningTask = null;
			}
		}
	}

	private void KeepScanning(VideoCapture camera)
	{
		PixelSize pixelSize = new(camera.FrameWidth, camera.FrameHeight);
		Vector dpi = new(96, 96);
		using WriteableBitmap writeableBitmap = new(pixelSize, dpi, PixelFormat.Rgba8888, AlphaFormat.Unpremul);

		int dataSize = camera.FrameWidth * camera.FrameHeight;
		int[] helperArray = new int[dataSize];
		using QRCodeDetector qRCodeDetector = new();
		using Mat frame = new();
		while (!_requestEnd)
		{
			try
			{
				bool gotBackFrame = camera.Read(frame);
				if (!gotBackFrame || frame.Width == 0 || frame.Height == 0)
				{
					continue;
				}
				ConvertMatToWriteableBitmap(frame, writeableBitmap, helperArray);

				NewImageArrived?.Invoke(this, writeableBitmap);

				if (qRCodeDetector.Detect(frame, out Point2f[] points))
				{
					using Mat tmpMat = new();
					string decodedText = qRCodeDetector.Decode(frame, points, tmpMat);
					if (string.IsNullOrWhiteSpace(decodedText))
					{
						continue;
					}
					if (AddressStringParser.TryParse(decodedText, Network, out _))
					{
						CorrectAddressFound?.Invoke(this, decodedText);
						break;
					}
					else
					{
						InvalidAddressFound?.Invoke(this, decodedText);
					}
				}
			}
			catch (OpenCVException)
			{
				throw new OpenCVException("Could not read frames. Please make sure no other program uses your camera.");
			}
		}
	}

	private void ConvertMatToWriteableBitmap(Mat frame, WriteableBitmap writeableBitmap, int[] helperArray)
	{
		using ILockedFramebuffer fb = writeableBitmap.Lock();
		Mat.Indexer<Vec3b> indexer = frame.GetGenericIndexer<Vec3b>();

		for (int y = 0; y < frame.Height; y++)
		{
			int rowIndex = y * fb.Size.Width;

			for (int x = 0; x < frame.Width; x++)
			{
				(byte r, byte g, byte b) = indexer[y, x];
				Color color = new(255, r, g, b);

				helperArray[rowIndex + x] = (int)color.ToUint32();
			}
		}

		Marshal.Copy(helperArray, 0, fb.Address, helperArray.Length);
	}
}
