using NBitcoin;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletCreationOptions()
{
	public record AddNewWallet(string? WalletName = null, string? Password = null, Mnemonic? Mnemonic = null) : WalletCreationOptions()
	{
		public AddNewWallet WithNewMnemonic()
		{
			return this with { Mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve) };
		}
	}

	public record ConnectToHardwareWallet(string? WalletName = null, HwiEnumerateEntry? Device = null) : WalletCreationOptions();

	public record ImportWallet(string? FilePath, string? WalletName = null) : WalletCreationOptions();

	public record RecoverWallet(string? WalletName = null, string? Password = null, Mnemonic? Mnemonic = null, int? MinGapLimit = null) : WalletCreationOptions();
}
