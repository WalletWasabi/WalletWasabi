namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletLoadWorkflow
{
	bool IsLoading { get; }

	void Start();

	void Stop();

	IObservable<(double PercentComplete, TimeSpan TimeRemaining)> Progress { get; }
}
