using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public record WalletStatusChangedEventArgs(object? Sender, Wallet Wallet, bool IsCoinJoining);
}
