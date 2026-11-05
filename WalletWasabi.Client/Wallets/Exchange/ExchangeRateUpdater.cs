using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.Client.Wallets.Exchange;

public static class ExchangeRateUpdater
{
	public static readonly string ServiceName = "ExchangeFeeRateUpdater";
	public record UpdateMessage;

	public static MessageHandler<UpdateMessage, decimal> CreateExchangeRateUpdater(
		ExchangeRateProvider provider, EventBus eventBus) =>
		(_, usdExchangeRate, cancellationToken) => UpdateExchangeRateAsync(usdExchangeRate, provider, eventBus, cancellationToken);

	private static async Task<decimal> UpdateExchangeRateAsync(decimal exchangeRate, ExchangeRateProvider provider, EventBus eventBus, CancellationToken cancellationToken)
	{
		var newExchangeRate = await provider(cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != exchangeRate && newExchangeRate.Rate > 0m)
		{
			exchangeRate = newExchangeRate.Rate;
			eventBus.Publish(new ExchangeRateChanged(newExchangeRate.Rate));
		}

		return exchangeRate;
	}
}
