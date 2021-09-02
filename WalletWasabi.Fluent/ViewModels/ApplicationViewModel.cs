using WalletWasabi.Fluent.Providers;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
	{
		bool ICanShutdownProvider.CanShutdown() => Services.WalletManager.AnyCoinJoinInProgress(); // => Services.HostedServices.Get<CoinJoinManager>().SystemAwakeChecker.CanShutDown
	}
}
