using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using Timer = System.Timers.Timer;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class HardwareWalletOperations : IDisposable
	{
		public event EventHandler<HwiEnumerateEntry[]>? DetectionCompleted;

		public HardwareWalletOperations(WalletManager walletManager)
		{
			WalletManager = walletManager;
			Client = new HwiClient(WalletManager.Network);
			DisposeCts = new CancellationTokenSource();
			PassphraseTimer = new Timer(8000) {AutoReset = false};
		}

		public WalletManager WalletManager { get; }

		public HwiClient Client { get; }

		private CancellationTokenSource? DetectionCts { get; set; }

		private CancellationTokenSource DisposeCts { get; }

		public Task? DetectionTask { get; set; }

		public Timer PassphraseTimer { get; }

		private void OnDetectionCompleted(HwiEnumerateEntry[] wallets)
		{
			DetectionCompleted?.Invoke(this, wallets);
		}

		public async Task<KeyManager> GenerateWalletAsync(string walletName, HwiEnumerateEntry device)
		{
			if (device.Fingerprint is null)
			{
				throw new Exception("Cannot be null.");
			}

			var fingerPrint = (HDFingerprint) device.Fingerprint;
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			var extPubKey = await Client.GetXpubAsync(
				device.Model,
				device.Path,
				KeyManager.DefaultAccountKeyPath,
				cts.Token).ConfigureAwait(false);
			var path = WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;

			return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, path);
		}

		public async Task InitHardwareWalletAsync(HwiEnumerateEntry device)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
			using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, DisposeCts.Token);

			// Trezor T doesn't require interactive mode.
			var interactiveMode = !(device.Model == HardwareWalletModels.Trezor_T || device.Model == HardwareWalletModels.Trezor_T_Simulator);

			try
			{
				await Client.SetupAsync(device.Model, device.Path, interactiveMode, initCts.Token);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		public async Task StopDetectionAsync()
		{
			if (DetectionTask is { } task && DetectionCts is { } cts)
			{
				cts.Cancel();
				await task;
			}
		}

		public void StartDetection()
		{
			if (DisposeCts is { } cts)
			{
				DetectionCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
				DetectionTask = HardwareWalletDetectionAsync(DetectionCts);
			}
		}

		protected async Task HardwareWalletDetectionAsync(CancellationTokenSource detectionCts)
		{
			try
			{
				using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

				PassphraseTimer.Start();
				var detectedHardwareWallets =
					(await Client.EnumerateAsync(timeoutCts.Token)
						.ConfigureAwait(false))
						.ToArray();

				detectionCts.Token.ThrowIfCancellationRequested();

				OnDetectionCompleted(detectedHardwareWallets);
			}
			catch (Exception ex)
			{
				if (ex is not OperationCanceledException)
				{
					Logger.LogError(ex);
				}
			}
			finally
			{
				PassphraseTimer.Stop();
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
				PassphraseTimer.Dispose();
			});
		}
	}
}
