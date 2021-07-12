using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using Avalonia;
using Avalonia.Media;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;
using NBitcoin;
using System.Threading;

namespace WalletWasabi.Fluent.Models
{
	public class WebcamQrReader
	{
		public bool RequestEnd { get; set; }
		public Network Network { get; }
		public Task? ScanningTask { get; set; }
		private Mat _frame;
		private QRCodeDetector _qRCodeDetector;
		private WriteableBitmap _writeableBitmap;

		public WebcamQrReader(Network network)
		{
			Network = network;
		}

		public void StartScanning()
		{
			if (ScanningTask is not { })
			{
				ScanningTask = Task.Run(() =>
				{
					VideoCapture? camera = null;
					try
					{
						camera = OpenCamera();
						RequestEnd = false;
						Scan(camera);
					}
					catch (Exception exc)
					{
						Logger.LogError("QR scanning stopped. Reason:", exc);
						ErrorOccured?.Invoke(this, exc);
					}
					finally
					{
						camera?.Release();
					}
				});
			}
		}

		public async Task StopScanningAsync()
		{
			if (ScanningTask is { } task)
			{
				RequestEnd = true;
				await task;
				ScanningTask = null;
			}
		}

		private void Scan(VideoCapture camera)
		{
			while (!RequestEnd)
			{
				try
				{
					_frame = new();
					camera.Read(_frame);
					if (_frame.Empty() || _frame.Width == 0 || _frame.Height == 0)
					{
						continue;
					}

					_writeableBitmap = ConvertMatToWriteableBitmap(_frame);
					NewImageArrived?.Invoke(this, _writeableBitmap);
					_qRCodeDetector = new();
					if (_qRCodeDetector.Detect(_frame, out Point2f[] points))
					{
						string qrCode = _qRCodeDetector.Decode(_frame, points, new Mat());
						if (!string.IsNullOrWhiteSpace(qrCode) && AddressStringParser.TryParse(qrCode, Network, out _))
						{
							BitcoinAddressFound?.Invoke(this, qrCode);
							break;
						}
					}
				}
				catch (OpenCVException exc)
				{
					Logger.LogWarning(exc);
				}
			}
			_frame.Dispose();
			_writeableBitmap.Dispose();
			_qRCodeDetector.Dispose();
		}

		private VideoCapture OpenCamera()
		{
			VideoCapture camera = new();
			if (!camera.Open(0))
			{
				throw new InvalidOperationException("Could not open webcam");
			}
			return camera;
		}

		private WriteableBitmap ConvertMatToWriteableBitmap(Mat frame)
		{
			PixelSize pixelSize = new(frame.Width, frame.Height);
			Vector dpi = new(96, 96);
			Avalonia.Platform.PixelFormat pixelFormat = Avalonia.Platform.PixelFormat.Rgba8888;
			Avalonia.Platform.AlphaFormat alphaFormat = Avalonia.Platform.AlphaFormat.Unpremul;
			var writeableBitmap = new WriteableBitmap(pixelSize, dpi, pixelFormat, alphaFormat);

			using (var fb = writeableBitmap.Lock())
			{
				var indexer = frame.GetGenericIndexer<Vec3b>();
				int[] data = new int[fb.Size.Width * fb.Size.Height];
				for (int y = 0; y < frame.Height; y++)
				{
					for (int x = 0; x < frame.Width; x++)
					{
						Vec3b pixel = indexer[y, x];
						byte r = pixel.Item0;
						byte g = pixel.Item1;
						byte b = pixel.Item2;
						var color = new Color(255, r, g, b);
						data[y * fb.Size.Width + x] = (int)color.ToUint32();
					}
				}
				Marshal.Copy(data, 0, fb.Address, fb.Size.Width * fb.Size.Height);
			}
			return writeableBitmap;
		}

		public event EventHandler<WriteableBitmap>? NewImageArrived;

		public event EventHandler<string>? BitcoinAddressFound;

		public event EventHandler<Exception>? ErrorOccured;
	}
}
