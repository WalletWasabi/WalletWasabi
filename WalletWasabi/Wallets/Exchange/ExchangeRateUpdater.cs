using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.Wallets.Exchange;

public static class ExchangeRateUpdater
{
	public static readonly string ServiceName = "ExchangeFeeRateUpdater";
	public record UpdateMessage;

	public static MessageHandler<UpdateMessage, decimal> CreateExchangeRateUpdater(
		ExchangeRateProvider provider, EventBus eventBus) =>
		(_, usdExchangeRate, cancellationToken) => UpdateExchangeRateAsync(usdExchangeRate, provider, eventBus, cancellationToken);

	private static async Task<decimal> UpdateExchangeRateAsync(decimal usdExchangeRate, ExchangeRateProvider provider, EventBus eventBus, CancellationToken cancellationToken)
	{
		var newExchangeRate = await provider(cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != usdExchangeRate && newExchangeRate.Rate > 0m)
		{
			usdExchangeRate = newExchangeRate.Rate;
			eventBus.Publish(new ExchangeRateChanged(newExchangeRate.Rate));
		}

		return usdExchangeRate;
	}
}
