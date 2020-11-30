using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class HardwareDetectionState
	{
		public HardwareDetectionState(string walletName, WalletManager walletManager, Network network)
		{
			WalletName = walletName;
			WalletManager = walletManager;
			Network = network;
			Client = new HwiClient(network);

			Devices = Enumerable.Empty<Hwi.Models.HwiEnumerateEntry>();
		}

		public IEnumerable<Hwi.Models.HwiEnumerateEntry> Devices { get; private set; }

		public Hwi.Models.HwiEnumerateEntry? SelectedDevice { get; set; }

		public WalletManager WalletManager { get; }

		public Network Network { get; }

		public HwiClient Client { get; }

		public string WalletName { get; }

		public async Task<KeyManager> GenerateWalletAsync()
		{
			var selectedDevice = SelectedDevice;

			if (selectedDevice?.Fingerprint is null)
			{
				throw new Exception("Cannot be null.");
			}

			var fingerPrint = (HDFingerprint) selectedDevice.Fingerprint;
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			var extPubKey = await Client.GetXpubAsync(
				selectedDevice.Model,
				selectedDevice.Path,
				KeyManager.DefaultAccountKeyPath,
				cts.Token).ConfigureAwait(false);
			var path = WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;

			return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, path);
		}

		public async Task EnumerateHardwareWalletsAsync(CancellationToken token)
		{
			Devices = (await Client.EnumerateAsync(token).ConfigureAwait(false))
				.Where(
					wallet => WalletManager.GetWallets()
				.Any(x => x.KeyManager.MasterFingerprint == wallet.Fingerprint) == false);
		}
	}
}
