using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Blockchain.Keys;

/// <summary>
/// SLIP-25: the coinjoin account hierarchy (m/10025') a hardware wallet keeps apart from its payment accounts,
/// so that only a coinjoin authorization can spend from it. Vendor-neutral; a device that signs coinjoins uses it.
/// <para>
/// A coinjoin authorization is bound to this account, so the coins and outputs of a round stay in it: the device counts
/// any other output as value leaving the account and refuses to sign the round, after the coordinator has its inputs,
/// which bans them. That rules out coinjoin payments and mixing to another wallet.
/// </para>
/// <para>
/// UnlockPath lets a transaction use the key paths of this account and no other own ones, so a regular transaction
/// spends either this account or the payment account, never both, and its change returns to the account it spends.
/// </para>
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
