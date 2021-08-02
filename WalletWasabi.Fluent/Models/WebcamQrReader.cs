using Avalonia.Media.Imaging;
using System;
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

namespace WalletWasabi.Fluent.Models
{
	public class WebcamQrReader
	{
		private const byte DefaultCameraId = 0;

		public WebcamQrReader(Network network)
		{
			ScanningTaskLock = new();
			Network = network;
		}

		public event EventHandler<WriteableBitmap>? NewImageArrived;

		public event EventHandler<string>? CorrectAddressFound;

		public event EventHandler<string>? InvalidAddressFound;

		public event EventHandler<Exception>? ErrorOccured;

		private AsyncLock ScanningTaskLock { get; set; }
		public bool RequestEnd { get; set; }
		public Network Network { get; }
		public Task? ScanningTask { get; set; }
		public bool IsRunning => ScanningTask is not null;

		public async Task StartScanningAsync()
		{
			using (await ScanningTaskLock.LockAsync().ConfigureAwait(false))
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					ErrorOccured?.Invoke(this, new NotImplementedException("This operating system is not supported."));
				}
				ScanningTask = Task.Run(() =>
				{
					VideoCapture? camera = null;
					try
					{
						camera = new();
						camera.SetExceptionMode(true);
						// Setting VIdeoCaptureAPI to DirectShow, to remove warning logs,
						// might need to be changed in the future for other operating systems
						if (!camera.Open(DefaultCameraId, VideoCaptureAPIs.DSHOW))
						{
							throw new InvalidOperationException("Could not open webcamera.");
						}
						RequestEnd = false;
						KeepScan(camera);
					}
					catch (Exception exc)
					{
						Logger.LogError("QR scanning stopped. Reason:", exc);
						ErrorOccured?.Invoke(this, exc);
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
					RequestEnd = true;
					await task;

					ScanningTask = null;
				}
			}
		}

		private void KeepScan(VideoCapture camera)
		{
			PixelSize pixelSize = new(camera.FrameWidth, camera.FrameHeight);
			Vector dpi = new(96, 96);
			using WriteableBitmap writeableBitmap = new(pixelSize, dpi, PixelFormat.Rgba8888, AlphaFormat.Unpremul);

			int dataSize = camera.FrameWidth * camera.FrameHeight;
			int[] helperArray = new int[dataSize];
			using QRCodeDetector qRCodeDetector = new();
			using Mat frame = new();
			while (!RequestEnd)
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
						string qrCode = qRCodeDetector.Decode(frame, points, tmpMat);
						if (string.IsNullOrWhiteSpace(qrCode))
						{
							continue;
						}
						if (AddressStringParser.TryParse(qrCode, Network, out _))
						{
							CorrectAddressFound?.Invoke(this, qrCode);
							break;
						}
						else
						{
							InvalidAddressFound?.Invoke(this, qrCode);
						}
					}
				}
				catch (OpenCVException exc)
				{
					Logger.LogWarning(exc);
					ErrorOccured?.Invoke(this, exc);
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
}
