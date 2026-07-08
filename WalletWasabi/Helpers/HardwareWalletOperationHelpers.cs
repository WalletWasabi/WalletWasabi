using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers;

public static class HardwareWalletOperationHelpers
{
	/// <summary>Whether the device model can act as a remote signer for coinjoins (SLIP-25).</summary>
	public static bool SupportsCoinJoin(this HwiEnumerateEntry device) => device.Model.SupportsCoinJoin();

	/// <param name="enableCoinjoin">When true, also fetches the SLIP-25 coinjoin account so the device can sign coinjoins. Requires the Trezor Bridge and a confirmation on the device.</param>
	public static async Task<KeyManager> GenerateWalletAsync(HwiEnumerateEntry device, string walletFilePath, Network network, CancellationToken cancelToken, bool enableCoinjoin = false)
	{
		if (device.Fingerprint is null)
		{
			throw new Exception("Fingerprint cannot be null.");
		}

		var fingerPrint = (HDFingerprint)device.Fingerprint;
		var segwitAccountKeyPath = KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit);

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var genCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		if (enableCoinjoin && device.SupportsCoinJoin())
		{
			// Coinjoin needs the SLIP-25 account, which only the bridge can read. Read the segwit account from the
			// bridge in the same session too, so HWI and the bridge don't contend for the USB device. If the bridge
			// is unavailable the whole import fails with a clear error instead of silently dropping coinjoin.
			using var trezor = await TrezorDevice.FindAsync(fingerPrint, genCts.Token).ConfigureAwait(false);
			var segwitFromBridge = await trezor.GetSegwitAccountXpubAsync(segwitAccountKeyPath, network, genCts.Token).ConfigureAwait(false);
			var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(network);
			var coinJoinFromBridge = await trezor.GetCoinJoinXpubAsync(coinJoinAccountKeyPath, network, genCts.Token).ConfigureAwait(false);

			return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, segwitFromBridge, coinJoinFromBridge, null, null, network, walletFilePath, coinJoinAccountKeyPath);
		}

		var client = new HwiClient(network);
		var segwitExtPubKey = await client.GetXpubAsync(device.Model, device.Path, segwitAccountKeyPath, genCts.Token).ConfigureAwait(false);
		return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, segwitExtPubKey, null, null, null, network, walletFilePath);
	}

	/// <summary>
	/// Adds a SLIP-25 coinjoin account to an already imported Trezor watch-only wallet, so it can start
	/// signing coinjoins. Requires the Trezor Bridge and a confirmation on the device. No-op if the wallet
	/// already has one. Throws <see cref="TrezorException"/> when the device or bridge is unavailable.
	/// </summary>
	public static async Task EnableCoinJoinAsync(KeyManager keyManager, Network network, CancellationToken cancelToken)
	{
		if (!keyManager.IsHardwareWallet)
		{
			throw new InvalidOperationException("Only a hardware wallet can have a coinjoin account added.");
		}
		if (keyManager.IsTrezorCoinJoinWallet())
		{
			return;
		}

		// The SLIP-25 account is only reachable through the Trezor Bridge. Any failure (bridge down, device
		// declined) throws TrezorException so the caller can tell the user, rather than silently doing nothing.
		using var trezor = await TrezorDevice.FindAsync(keyManager.MasterFingerprint, cancelToken).ConfigureAwait(false);
		var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(network);
		var coinJoinExtPubKey = await trezor.GetCoinJoinXpubAsync(coinJoinAccountKeyPath, network, cancelToken).ConfigureAwait(false);

		keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, coinJoinExtPubKey);
	}

	public static async Task InitHardwareWalletAsync(HwiEnumerateEntry device, Network network, CancellationToken cancelToken)
	{
		var client = new HwiClient(network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
		using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		// Trezor T doesn't require interactive mode.
		var interactiveMode = !(device.Model == HardwareWalletModels.Trezor_T || device.Model == HardwareWalletModels.Trezor_T_Simulator);

		try
		{
			await client.SetupAsync(device.Model, device.Path, interactiveMode, initCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public static async Task<HwiEnumerateEntry[]> DetectAsync(Network network, CancellationToken cancelToken)
	{
		// HWI needs exclusive USB access, which a coinjoin bridge we started would hold. Release it so
		// detection works; a loaded coinjoin wallet restarts the bridge the next time it needs to sign.
		TrezorBridgeManager.StopIfOurs();

		var client = new HwiClient(network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		var detectedHardwareWallets = (await client.EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).ToArray();

		cancelToken.ThrowIfCancellationRequested();

		return detectedHardwareWallets;
	}
}
