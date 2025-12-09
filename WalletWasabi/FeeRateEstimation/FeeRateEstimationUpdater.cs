using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.FeeRateEstimation;

public static class FeeRateEstimationUpdater
{
	public static readonly string ServiceName = "FeeRateUpdater";
	public record UpdateMessage;

	public static MessageHandler<UpdateMessage, FeeRateEstimations> CreateUpdater(FeeRateProvider feeRateProvider, EventBus eventBus) =>
		(message, feeRateEstimations, cancellationToken) => UpdateAsync(message, feeRateProvider, feeRateEstimations, eventBus, cancellationToken);

	private static async Task<FeeRateEstimations> UpdateAsync(UpdateMessage _, FeeRateProvider feeRateProvider, FeeRateEstimations feeRateEstimations, EventBus eventBus, CancellationToken cancellationToken)
	{
		var newFeeRateEstimations = await feeRateProvider(cancellationToken).ConfigureAwait(false);
		if (newFeeRateEstimations != feeRateEstimations)
		{
			feeRateEstimations = newFeeRateEstimations;
			eventBus.Publish(new MiningFeeRatesChanged(newFeeRateEstimations));
		}

		return feeRateEstimations;
	}
}
