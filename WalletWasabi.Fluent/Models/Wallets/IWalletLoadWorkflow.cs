using System.Reactive;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletLoadWorkflow
{
	bool IsLoading { get; }
	IObservable<(double PercentComplete, TimeSpan TimeRemaining)> Progress { get; }
	IObservable<Unit> LoadCompleted { get; }
	void Start();
	void Stop();
}
