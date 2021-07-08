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

namespace WalletWasabi.Fluent.Models
{
	public class WebcamQrReader
	{
		public Network Network { get; }
		public Task? ScanningTask { get; set; }

		public WebcamQrReader(Network network)
		{
			Network = network;
		}

		public void StartScanning()
		{
			ScanningTask = Task.Run(() =>
			{
				VideoCapture camera = OpenCamera();
				Scan(camera);
			});
		}

		private void Scan(VideoCapture camera)
		{
			while (camera is not null)
			{
				try
				{
					using Mat frame = new();
					camera.Read(frame);
					if (frame.Empty() || frame.Width == 0 || frame.Height == 0)
					{
						continue;
					}

					using var writeableBitmap = ConvertMatToWriteableBitmap(frame);
					NewImageArrived?.Invoke(this, writeableBitmap);
					using QRCodeDetector qRCodeDetector = new();
					if (qRCodeDetector.Detect(frame, out Point2f[] points))
					{
						string qrCode = qRCodeDetector.Decode(frame, points, new Mat());
						if (!string.IsNullOrWhiteSpace(qrCode) && AddressStringParser.TryParse(qrCode, Network, out _))
						{
							BitcoinAddressFound?.Invoke(this, qrCode);
						}
					}
				}
				catch (OpenCVException exc)
				{
					Logger.LogWarning(exc);
				}
			}
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
	}
}
