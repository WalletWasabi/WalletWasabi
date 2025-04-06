namespace WalletWasabi.Fluent.Models;

public abstract record WalletBackupType(WalletBackupTypeOptions Options)
{
	public record RecoveryWords(WalletBackupTypeOptions Options) : WalletBackupType(Options);

	public record MultiShare(WalletBackupTypeOptions Options) : WalletBackupType(Options);
}
