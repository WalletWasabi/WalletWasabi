using ReactiveUI;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class CoinjoinModel : ReactiveObject
{
	private readonly IServices _services;

	public CoinjoinModel(IServices services)
	{
		_services = services;
	}

	public bool CanShutdown()
	{
		return _services.GetHighestCoinJoinClientState() switch
		{
			CoinJoinClientState.InCriticalPhase => false,
			CoinJoinClientState.Idle or CoinJoinClientState.InSchedule or CoinJoinClientState.InProgress => true,
			_ => throw new ArgumentOutOfRangeException(),
		};
	}

	public Task RestartAbortedCoinjoinsAsync() => _services.RestartAbortedCoinjoinsAsync();

	public Task SignalToStopCoinjoinsAsync() => _services.SignalToStopCoinjoinsAsync();
}
