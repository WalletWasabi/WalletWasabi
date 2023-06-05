namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletLoadWorkflow
{
	bool IsLoading { get; }

	IObservable<(double PercentComplete, TimeSpan TimeRemaining)> Progress { get; }

	void Start();

	void Stop();
}
