using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Services;

namespace WalletWasabi.FeeRateEstimation;

public class FeeRateEstimationUpdater(TimeSpan period, FeeRateProvider feeRateProvider, EventBus eventBus)
	: PeriodicRunner(period)
{
	public FeeRateEstimations? FeeEstimates { get; private set; }

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newFeeRateEstimations = await feeRateProvider(cancellationToken).ConfigureAwait(false);
		if (newFeeRateEstimations != FeeEstimates)
		{
			FeeEstimates = newFeeRateEstimations;
			eventBus.Publish(new MiningFeeRatesChanged(newFeeRateEstimations));
		}

		await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(120)), cancellationToken).ConfigureAwait(false);
	}
}
