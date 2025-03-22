using NBitcoin;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletCreationOptions(string? WalletName = null)
{
	public record AddNewWallet(string? WalletName = null, WalletBackup? WalletBackup = null)
		: WalletCreationOptions(WalletName)
	{
		public AddNewWallet WithNewMnemonic()
		{
			if (WalletBackup is not null && WalletBackup is not RecoveryWordsBackup)
			{
				throw new ArgumentException("Cannot create a new mnemonic with a non-recovery words backup.");
			}

			var recoveryWordsBackup = WalletBackup as RecoveryWordsBackup ?? new RecoveryWordsBackup();

			recoveryWordsBackup = recoveryWordsBackup with
			{
				Mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve)
			};

			return this with { WalletBackup = recoveryWordsBackup };
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
