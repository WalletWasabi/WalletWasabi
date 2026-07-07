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

		var client = new HwiClient(network);
		var fingerPrint = (HDFingerprint)device.Fingerprint;

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var genCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		var segwitExtPubKey = await client.GetXpubAsync(
			device.Model,
			device.Path,
			KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit),
			genCts.Token).ConfigureAwait(false);

		ExtPubKey? coinJoinExtPubKey = null;
		KeyPath? coinJoinAccountKeyPath = null;

		// The coinjoin account is opt-in: it is only added when the user asks for it, because fetching it
		// prompts the device and only coinjoin capable models support it.
		if (enableCoinjoin && device.SupportsCoinJoin())
		{
			(coinJoinAccountKeyPath, coinJoinExtPubKey) = await GetCoinJoinAccountAsync(fingerPrint, network, genCts.Token).ConfigureAwait(false);
		}

		return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, segwitExtPubKey, coinJoinExtPubKey, null, null, network, walletFilePath, coinJoinAccountKeyPath);
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

		var (coinJoinAccountKeyPath, coinJoinExtPubKey) = await GetCoinJoinAccountAsync(keyManager.MasterFingerprint, network, cancelToken).ConfigureAwait(false);
		if (coinJoinExtPubKey is null || coinJoinAccountKeyPath is null)
		{
			throw new TrezorException("Could not read the coinjoin account from the device.");
		}

		keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, coinJoinExtPubKey);
	}

	private static async Task<(KeyPath?, ExtPubKey?)> GetCoinJoinAccountAsync(HDFingerprint? fingerPrint, Network network, CancellationToken cancelToken)
	{
		// The SLIP-25 account is only reachable through the Trezor Bridge (HWI cannot unlock it), so this is
		// skipped with a warning when the bridge is not running instead of failing the whole wallet creation.
		try
		{
			using var trezor = await TrezorDevice.FindAsync(fingerPrint, cancelToken).ConfigureAwait(false);
			var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(network);
			var coinJoinExtPubKey = await trezor.GetCoinJoinXpubAsync(coinJoinAccountKeyPath, network, cancelToken).ConfigureAwait(false);
			return (coinJoinAccountKeyPath, coinJoinExtPubKey);
		}
		catch (TrezorException e)
		{
			Logger.LogWarning($"Could not add a coinjoin account to the wallet: {e.Message}");
			return (null, null);
		}
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
		var client = new HwiClient(network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		var detectedHardwareWallets = (await client.EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).ToArray();

		cancelToken.ThrowIfCancellationRequested();

		return detectedHardwareWallets;
	}
}
