using ReactiveUI;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class CoinjoinModel : ReactiveObject
{
	private readonly IServices _services;
	private CoinJoinManager? _coinJoinManager;

	public CoinjoinModel(IServices services)
	{
		_services = services;
	}

	public bool CanShutdown()
	{
		var cjManager = GetCoinjoinManager();

		if (cjManager is { })
		{
			return cjManager.HighestCoinJoinClientState switch
			{
				CoinJoinClientState.InCriticalPhase => false,
				CoinJoinClientState.Idle or CoinJoinClientState.InSchedule or CoinJoinClientState.InProgress => true,
				_ => throw new ArgumentOutOfRangeException(),
			};
		}

		return true;
	}

	public async Task RestartAbortedCoinjoinsAsync()
	{
		var cjManager = GetCoinjoinManager();
		if (cjManager is { })
		{
			await cjManager.RestartAbortedCoinjoinsAsync();
		}
	}

	public async Task SignalToStopCoinjoinsAsync()
	{
		var cjManager = GetCoinjoinManager();
		if (cjManager is { })
		{
			await cjManager.SignalToStopCoinjoinsAsync();
		}
	}

	private CoinJoinManager? GetCoinjoinManager()
	{
		return _coinJoinManager ??= _services.GetHostedService<CoinJoinManager>();
	}
}
