namespace WalletWasabi.Tor.NetworkChecker;

public interface ITorNetwork
{
    public IObservable<Issue> Issues { get; }
}
