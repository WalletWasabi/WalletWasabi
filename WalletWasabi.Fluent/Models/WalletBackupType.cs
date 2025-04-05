namespace WalletWasabi.Fluent.Models;

public abstract record WalletBackupType(string Description, string HelpText, string ToolTipText)
{
	public record RecoveryWords() : WalletBackupType(
		Description: "Recovery words Backup",
		HelpText: "Back up your wallet using a set of secret words. Write them down and store them safely — you’ll need them to recover your wallet.",
		ToolTipText: "Creates a BIP39 mnemonic phrase (typically 12 or 24 words) that encodes the wallet's seed. This phrase can regenerate your private keys and restore access to your funds on compatible wallets.");

	public record MultiShare() : WalletBackupType(
		Description: "Multi-share Backup",
		HelpText: "Split your wallet backup into multiple parts. You’ll need some of them to restore your wallet, adding an extra layer of protection.",
		ToolTipText: "Uses Shamir's Secret Sharing (SLIP-0039) to divide the wallet secret into multiple shares. A defined number of these shares are required to reconstruct the wallet’s master secret.");
}
