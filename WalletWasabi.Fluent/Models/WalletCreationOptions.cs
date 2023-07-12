using NBitcoin;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletCreationOptions(string? WalletName = null)
{
	public record AddNewWallet(string? WalletName = null, string? Password = null, Mnemonic? Mnemonic = null) : WalletCreationOptions(WalletName)
	{
		public AddNewWallet WithNewMnemonic()
		{
			return this with { Mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve) };
		}
	}

	public record ConnectToHardwareWallet(string? WalletName = null, HwiEnumerateEntry? Device = null) : WalletCreationOptions(WalletName);

	public record ImportWallet(string? WalletName = null, string? FilePath = null) : WalletCreationOptions(WalletName);

	public record RecoverWallet(string? WalletName = null, string? Password = null, Mnemonic? Mnemonic = null, int? MinGapLimit = null) : WalletCreationOptions(WalletName);
}
