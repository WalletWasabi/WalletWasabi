namespace WalletWasabi.Fluent.Models;

public abstract record WalletBackupType(string Description)
{
	public record RecoveryWords() : WalletBackupType("Recovery words Backup");

	public record MultiShare() : WalletBackupType("Multi-share Backup");
}
