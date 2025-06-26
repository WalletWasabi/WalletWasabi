using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.FeeRateEstimation;

public static class FeeRateEstimationUpdater
{
	public abstract record Message;
	public record UpdateMessage : Message;
	public record GetMessage(IReplyChannel<FeeRateEstimations> ReplyChannel) : Message;

	public static MessageHandler<Message, FeeRateEstimations> CreateUpdater(FeeRateProvider feeRateProvider, EventBus eventBus) =>
		(message, feeRateEstimations, cancellationToken) => UpdateAsync(message, feeRateProvider, feeRateEstimations, eventBus, cancellationToken);

	private static async Task<FeeRateEstimations> UpdateAsync(Message message, FeeRateProvider feeRateProvider, FeeRateEstimations feeRateEstimations, EventBus eventBus, CancellationToken cancellationToken)
	{
		switch (message)
		{
			case UpdateMessage:
			{
				var newFeeRateEstimations = await feeRateProvider(cancellationToken).ConfigureAwait(false);
				if (newFeeRateEstimations != feeRateEstimations)
				{
					feeRateEstimations = newFeeRateEstimations;
					eventBus.Publish(new MiningFeeRatesChanged(newFeeRateEstimations));
				}

				break;
			}
			case GetMessage getter:
				getter.ReplyChannel.Reply(feeRateEstimations);
				break;
		}

		return feeRateEstimations;
	}
}
