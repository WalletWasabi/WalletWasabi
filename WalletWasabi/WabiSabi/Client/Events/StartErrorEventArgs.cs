using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Events;

public class StartErrorEventArgs : StatusChangedEventArgs
{
	public StartErrorEventArgs(Wallet wallet, CoinjoinError error)
		: base(wallet)
	{
		Error = error;
	}

	public CoinjoinError Error { get; }
}
