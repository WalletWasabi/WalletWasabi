using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class HardwareWalletOperations : IDisposable
	{
		public HardwareWalletOperations(Network network)
		{
			Client = new HwiClient(network);
			DisposeCts = new CancellationTokenSource();
		}

		public HwiClient Client { get; }

		private CancellationTokenSource DisposeCts { get; }

		public async Task<KeyManager> GenerateWalletAsync(HwiEnumerateEntry device, string walletFilePath)
		{
			if (device.Fingerprint is null)
			{
				throw new Exception("Fingerprint cannot be null.");
			}

			var fingerPrint = (HDFingerprint)device.Fingerprint;
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			using var genCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, DisposeCts.Token);

			var extPubKey = await Client.GetXpubAsync(
				device.Model,
				device.Path,
				KeyManager.DefaultAccountKeyPath,
				genCts.Token).ConfigureAwait(false);

			return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, walletFilePath);
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

		public async Task<HwiEnumerateEntry[]> DetectAsync()
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, DisposeCts.Token);

			var detectedHardwareWallets = (await Client.EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).ToArray();

			DisposeCts.Token.ThrowIfCancellationRequested();

			return detectedHardwareWallets;
		}

		public void Dispose()
		{
			DisposeCts.Cancel();
			DisposeCts.Dispose();
		}
	}
}
