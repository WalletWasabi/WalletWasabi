using NBitcoin;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletCreationOptions(string? WalletName = null)
{
	public record AddNewWallet(string? WalletName = null, WalletBackup? SelectedWalletBackup = null, WalletBackup[]? WalletBackups = null)
		: WalletCreationOptions(WalletName)
	{
		public AddNewWallet WithNewWalletBackups()
		{
			var recoveryWordsBackup = new RecoveryWordsBackup
			{
				Mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve)
			};

			var multiShareBackupSettings = new MultiShareBackupSettings();

			var multiShareBackup = new MultiShareBackup(new MultiShareBackupSettings())
			{
				// TODO:
#if true
				Shares = Shamir.Generate(
					multiShareBackupSettings.Threshold,
					multiShareBackupSettings.Shares,
					RandomUtils.GetBytes(256 / 8))
#else
				// TODO: Debug code
				Shares = Shamir.Generate(
					groupThreshold: 2,
					groups:
					[
						// Alice group shares. 1 is enough to reconstruct a group share,
						// therefore she needs at least two group shares to be reconstructed,
						(1, 1),
						(1, 1),
						// 3 of 5 Friends' shares are required to reconstruct this group share
						(3, 5),
						// 2 of 6 Family's shares are required to reconstruct this group share
						(2, 6),
					],
					seed: "ABCDEFGHIJKLMNOP"u8.ToArray(),
					passphrase: "TREZOR")
#endif
			};

			return this with
			{
				SelectedWalletBackup = recoveryWordsBackup,
				WalletBackups = [recoveryWordsBackup, multiShareBackup]
			};
		}
	}

	public record ConnectToHardwareWallet(
		string? WalletName = null,
		HwiEnumerateEntry? Device = null) : WalletCreationOptions(WalletName);

	public record ImportWallet(
		string? WalletName = null,
		string? FilePath = null) : WalletCreationOptions(WalletName);

	public record RecoverWallet(
		string? WalletName = null,
		WalletBackup? WalletBackup = null,
		int? MinGapLimit = null) : WalletCreationOptions(WalletName);
}
