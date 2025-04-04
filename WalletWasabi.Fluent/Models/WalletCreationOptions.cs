using NBitcoin;
using WalletWasabi.Blockchain.Keys;
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
				Shares = Shamir.Generate(
					multiShareBackupSettings.Threshold,
					multiShareBackupSettings.Shares,
					WalletGenerator.GenerateShamirEntropy())
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
