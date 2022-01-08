using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.CoinJoin.Coordinator;

public class RoundBootstrapper : PeriodicRunner
{
	public RoundBootstrapper(TimeSpan period, Coordinator coordinator) : base(period)
	{
		Coordinator = coordinator;
	}

	public Coordinator Coordinator { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		await Coordinator.MakeSureInputregistrableRoundRunningAsync().ConfigureAwait(false);
	}
}
