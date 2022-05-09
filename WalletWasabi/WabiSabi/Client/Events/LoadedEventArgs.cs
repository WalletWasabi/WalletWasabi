using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Events;

public class LoadedEventArgs : StatusChangedEventArgs
{
	public LoadedEventArgs(Wallet wallet)
		: base(wallet)
	{
	}
}
