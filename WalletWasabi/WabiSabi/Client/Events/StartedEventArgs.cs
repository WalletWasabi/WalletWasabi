using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Events;

public class StartedEventArgs : StatusChangedEventArgs
{
	public StartedEventArgs(Wallet wallet, TimeSpan registrationTimeout)
		: base(wallet)
	{
		RegistrationTimeout = registrationTimeout;
	}

	public TimeSpan RegistrationTimeout { get; }
}
