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
using WalletWasabi.Helpers;
using System.Diagnostics;
using WalletWasabi.Microservices;
using System.Threading;

namespace WalletWasabi.Fluent.Models
{
	public class WebcamQrReader
	{
		private const byte DefaultCameraId = 0;

		public WebcamQrReader(Network network)
		{
			Network = network;
		}

		public event EventHandler<WriteableBitmap>? NewImageArrived;

		public event EventHandler<string>? CorrectAddressFound;

		public event EventHandler<string>? InvalidAddressFound;

		public event EventHandler<Exception>? ErrorOccured;

		private AsyncLock ScanningTaskLock { get; } = new();
		private bool RequestEnd { get; set; }
		private Network Network { get; }
		private Task? ScanningTask { get; set; }

		public static bool IsOsPlatformSupported()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return true;
			}
			return false;
		}

		public async Task StartScanningAsync()
		{
			using (await ScanningTaskLock.LockAsync().ConfigureAwait(false))
			{
				if (ScanningTask is { })
				{
					return;
				}
				RequestEnd = false;
				ScanningTask = Task.Run(async () =>
				{
					VideoCapture? camera = null;
					try
					{
						if (!IsOsPlatformSupported())
						{
							throw new NotImplementedException("This operating system is not supported.");
						}
						camera = new();
						camera.SetExceptionMode(true);
						// Setting VideoCaptureAPI to DirectShow, to remove warning logs,
						// might need to be changed in the future for other operating systems
						if (!camera.Open(DefaultCameraId, VideoCaptureAPIs.ANY))
						{
							throw new InvalidOperationException("Could not open webcamera.");
						}
						KeepScanning(camera);
					}
					catch (Exception ex)
					{
						Logger.LogError("QR scanning stopped. Reason:", ex);
						if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
						{
							HandleOSXAsync();
						}
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

		private async void HandleOSXAsync()
		{
			bool isBrewInstalled = await CheckIfBrewIsInstalledAsync();
			if (!isBrewInstalled)
			{
				InstallBrewAsync();
				InstallDependencies();
			}
		}

		private void InstallDependencies()
		{
			// It looks like if you uninstall brew, all things installed via brew is removed as well.
			// This way, if brew is not installed, we can tell 99% sure that neither the dependencies are.
			// TODO: Finish depencency installment function
			throw new NotImplementedException();
		}

		private async void InstallBrewAsync()
		{
			// This command installs brew, that could be the most optimal, but may not be the handiest.
			// After testing, cloning the og repo and running brew there is better, as it doesn't require a password from the user.
			// TODO: Replace this with cloning the repository
			var command = "/bin/bash -c \"$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh) \"";
			await EnvironmentHelpers.ShellExecAsync(command).ConfigureAwait(false);
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

		private void KeepScanning(VideoCapture camera)
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
				catch (OpenCVException ex)
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

		public static async Task<bool> CheckIfBrewIsInstalledAsync()
		{
			string argument = "brew --version";
			string brewPath = "path/to/brew/repository/bin/brew"; //Insert brew's path after cloning repository
			var startInfo = new ProcessStartInfo
			{
				FileName = brewPath,
				Arguments = argument,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = false,
				WindowStyle = ProcessWindowStyle.Normal
			};

			using var process = new ProcessAsync(startInfo);

			process.Start();

			string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			return output.Contains("Homebrew"); //Replace this with something more correct to search for
		}
	}
}
