namespace WalletWasabi.Fluent.AppServices.Tor;

public interface ITorNetwork
{
	IObservable<Issue> Issues { get; }
}
