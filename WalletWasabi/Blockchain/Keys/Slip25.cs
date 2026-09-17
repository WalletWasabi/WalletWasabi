using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Blockchain.Keys;

/// <summary>
/// SLIP-25: the coinjoin account hierarchy (m/10025') a hardware wallet keeps apart from its payment accounts,
/// so that only a coinjoin authorization can spend from it. Vendor-neutral; a device that signs coinjoins uses it.
/// </summary>
public static class Slip25
{
	private const uint HardenedIndex = 0x80000000;

	/// <summary>Purpose index 10025', enforced by the device firmware for every coinjoin.</summary>
	public const uint Purpose = 10025 | HardenedIndex;

	/// <summary>Coinjoin account: m/10025'/coin_type'/account'/1' where 1' stands for taproot.</summary>
	public static KeyPath GetCoinJoinAccountKeyPath(Network network) =>
		new(Purpose, (network == Network.Main ? 0u : 1u) | HardenedIndex, HardenedIndex, 1u | HardenedIndex);

	public static bool IsSlip25KeyPath(this KeyPath keyPath) =>
		keyPath.Indexes is [Purpose, ..];

	/// <summary>Whether a detected device can sign coinjoins from a SLIP-25 account, to offer it while importing.</summary>
	public static bool SupportsCoinJoin(this HardwareWalletModels model) =>
		model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_T_Simulator or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5;
}
