using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class HardwareWalletOperations : IDisposable
	{
		public event EventHandler<HwiEnumerateEntry[]>? HardwareWalletsFound;

		public HardwareWalletOperations(WalletManager walletManager, Network network)
		{
			WalletManager = walletManager;
			Network = network;
			Client = new HwiClient(network);

			DisposeCts = new CancellationTokenSource();

			StartDetection();
		}

		public HwiEnumerateEntry? SelectedDevice { get; set; }

		public WalletManager WalletManager { get; }

		public Network Network { get; }

		public HwiClient Client { get; }

		private CancellationTokenSource? DetectionCts { get; set; }

		private CancellationTokenSource DisposeCts { get; }

		public Task? DetectionTask { get; set; }

		private void OnHardwareWalletsFound(HwiEnumerateEntry[] wallet)
		{
			HardwareWalletsFound?.Invoke(this,wallet);
		}

		public async Task GenerateWalletAsync(string walletName)
		{
			var selectedDevice = SelectedDevice;

			try
			{
				if (selectedDevice?.Fingerprint is null)
				{
					throw new Exception("Cannot be null.");
				}

				await StopDetectionAsync();

				var fingerPrint = (HDFingerprint) selectedDevice.Fingerprint;
				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
				var extPubKey = await Client.GetXpubAsync(
					selectedDevice.Model,
					selectedDevice.Path,
					KeyManager.DefaultAccountKeyPath,
					cts.Token).ConfigureAwait(false);
				var path = WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;

				var km = KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, path);
				WalletManager.AddWallet(km);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				StartDetection();
			}
		}

		private async Task StopDetectionAsync()
		{
			if (DetectionTask is { } task && DetectionCts is { } cts)
			{
				cts.Cancel();
				await task;
			}
		}

		private void StartDetection()
		{
			if (DisposeCts is { } cts)
			{
				DetectionCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
				DetectionTask = HardwareWalletDetectionAsync(DetectionCts);
			}
		}

		protected async Task HardwareWalletDetectionAsync(CancellationTokenSource detectionCts)
		{
			while (!detectionCts.IsCancellationRequested)
			{
				var sw = Stopwatch.StartNew();

				try
				{
					using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var detectedHardwareWallets =
						(await Client.EnumerateAsync(timeoutCts.Token)
							.ConfigureAwait(false))
							.Where(wallet => WalletManager.GetWallets()
							.Any(x => x.KeyManager.MasterFingerprint == wallet.Fingerprint) == false)
							.ToArray();

					detectionCts.Token.ThrowIfCancellationRequested();

					if (detectedHardwareWallets.Length > 0)
					{
						OnHardwareWalletsFound(detectedHardwareWallets);
					}
				}
				catch (Exception ex)
				{
					if (ex is not OperationCanceledException)
					{
						Logger.LogError(ex);
					}
				}

				// Too fast enumeration causes the detected hardware wallets to be unable to provide the fingerprint.
				// Wait at least 5 seconds between two enumerations.
				sw.Stop();
				if (sw.Elapsed.Milliseconds < 5000)
				{
					await Task.Delay(5000 - sw.Elapsed.Milliseconds).ConfigureAwait(false);
				}
			}
		}

		public void Dispose()
		{
			Task.Run(async () =>
			{
				await StopDetectionAsync();

				DisposeCts.Dispose();
				DetectionCts?.Dispose();
				DetectionTask?.Dispose();
			});
		}
	}
}
