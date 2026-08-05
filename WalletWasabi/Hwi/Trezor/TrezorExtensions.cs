using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Hwi.Trezor;

public static class TrezorExtensions
{
	/// <summary>A hardware wallet whose taproot account is a SLIP-25 coinjoin account, created from a coinjoin capable Trezor.</summary>
	public static bool IsTrezorCoinJoinWallet(this KeyManager keyManager) =>
		keyManager.IsHardwareWallet && keyManager.TaprootAccountKeyPath.Indexes is [TrezorDevice.Slip25Purpose, ..];

	public static KeyPath? TryGetKeyPath(this KeyManager keyManager, Script scriptPubKey) =>
		keyManager.GetKeys(key => key.ContainsScript(scriptPubKey)).FirstOrDefault()?.FullKeyPath;

	public static bool IsSlip25KeyPath(this KeyPath keyPath) =>
		keyPath.Indexes is [TrezorDevice.Slip25Purpose, ..];

	public static bool SupportsCoinJoin(this HardwareWalletModels model) =>
		model is HardwareWalletModels.Trezor_T
			or HardwareWalletModels.Trezor_T_Simulator
			or HardwareWalletModels.Trezor_Safe_3
			or HardwareWalletModels.Trezor_Safe_5;
}
