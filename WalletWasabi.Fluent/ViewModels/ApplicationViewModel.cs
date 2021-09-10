using System;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
	{
		bool ICanShutdownProvider.CanShutdown()
		{
			return Services.HostedServices.Get<CoinJoinManager>().HighestCoinJoinClientState switch
			{
				CoinJoinClientState.InCriticalPhase => false,
				CoinJoinClientState.Idle or CoinJoinClientState.InProgress => true,
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}
