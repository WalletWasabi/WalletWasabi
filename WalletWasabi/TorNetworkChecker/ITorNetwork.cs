namespace WalletWasabi.TorNetworkChecker;

public interface ITorNetwork
{
    public IObservable<Issue> Issues { get; }
}
