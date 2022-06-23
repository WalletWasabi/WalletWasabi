namespace WalletWasabi.Tor.NetworkChecker;

public interface ITorNetwork
{
	IObservable<Issue> Issues { get; }
}
