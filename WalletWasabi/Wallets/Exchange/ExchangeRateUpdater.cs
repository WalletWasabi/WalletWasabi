using WalletWasabi.Services;

namespace WalletWasabi.Wallets.Exchange;

public static class ExchangeRateUpdater
{
	public static readonly string ServiceName = "ExchangeFeeRateUpdater";
	public record UpdateMessage;

	public static MessageHandler<UpdateMessage, decimal> CreateExchangeRateUpdater(
		ExchangeRateProvider provider, EventBus eventBus) =>
		(_, exchangeRate, cancellationToken) => UpdateExchangeRateAsync(exchangeRate, provider, eventBus, cancellationToken);

	private static async Task<decimal> UpdateExchangeRateAsync(decimal exchangeRate, ExchangeRateProvider provider, EventBus eventBus, CancellationToken cancellationToken)
	{
		var newExchangeRate = await provider(cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != exchangeRate && newExchangeRate.Rate > 0m)
		{
			exchangeRate = newExchangeRate.Rate;
			eventBus.Publish(new ExchangeRateChanged(newExchangeRate));
		}

		return exchangeRate;
	}
}
