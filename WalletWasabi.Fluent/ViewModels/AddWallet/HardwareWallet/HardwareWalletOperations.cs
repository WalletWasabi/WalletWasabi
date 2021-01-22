using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using Timer = System.Timers.Timer;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class HardwareWalletOperations : IAsyncDisposable
	{
		public event EventHandler<HwiEnumerateEntry[]>? DetectionCompleted;

		public event EventHandler? PassphraseNeeded;

		public HardwareWalletOperations(Network network)
		{
			Client = new HwiClient(network);
			DisposeCts = new CancellationTokenSource();
		}

		public HwiClient Client { get; }

		private CancellationTokenSource DisposeCts { get; }

		public Task? DetectionTask { get; set; }

		public Task? InitTask { get; set; }

		private void OnDetectionCompleted(HwiEnumerateEntry[] wallets)
		{
			DetectionCompleted?.Invoke(this, wallets);
		}

		private void OnPassphraseNeeded(object sender, ElapsedEventArgs e)
		{
			PassphraseNeeded?.Invoke(this, EventArgs.Empty);
		}

		public async Task<KeyManager> GenerateWalletAsync(HwiEnumerateEntry device, string walletFilePath)
		{
			if (device.Fingerprint is null)
			{
				throw new Exception("Fingerprint cannot be null.");
			}

			var fingerPrint = (HDFingerprint)device.Fingerprint;
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			var extPubKey = await Client.GetXpubAsync(
				device.Model,
				device.Path,
				KeyManager.DefaultAccountKeyPath,
				cts.Token).ConfigureAwait(false);

			return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, walletFilePath);
		}

		public void InitHardwareWallet(HwiEnumerateEntry device)
		{
			if (InitTask?.Status == TaskStatus.WaitingForActivation)
			{
				return;
			}

			InitTask = InitHardwareWalletAsync(device);
		}

		private async Task InitHardwareWalletAsync(HwiEnumerateEntry device)
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

		public void StartDetection()
		{
			if (DetectionTask?.Status == TaskStatus.WaitingForActivation)
			{
				return;
			}

			DetectionTask = RunDetectionAsync();
		}

		protected async Task RunDetectionAsync()
		{
			using var passphraseTimer = new Timer(8000) { AutoReset = false };
			passphraseTimer.Elapsed += OnPassphraseNeeded;

			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, DisposeCts.Token);

				passphraseTimer.Start();
				var detectedHardwareWallets =
					(await Client.EnumerateAsync(timeoutCts.Token)
						.ConfigureAwait(false))
						.ToArray();

				DisposeCts.Token.ThrowIfCancellationRequested();

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
				passphraseTimer.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (DetectionTask is { } detectionTask)
			{
				DisposeCts.Cancel();
				await detectionTask;
				detectionTask.Dispose();
			}

			if (InitTask is { } initTask)
			{
				DisposeCts.Cancel();
				await initTask;
				initTask.Dispose();
			}

			DisposeCts.Dispose();
		}
	}
}
