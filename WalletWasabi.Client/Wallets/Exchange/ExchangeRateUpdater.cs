using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.Client.Wallets.Exchange;

public static class ExchangeRateUpdater
{
	public static readonly string ServiceName = "ExchangeFeeRateUpdater";
	public record UpdateMessage;

	public static MessageHandler<UpdateMessage, ExchangeRate[]> CreateExchangeRateUpdater(
		ExchangeRateProvider provider, EventBus eventBus) =>
		(_, exchangeRates, cancellationToken) => UpdateExchangeRatesAsync(exchangeRates, provider, eventBus, cancellationToken);

	private static async Task<ExchangeRate[]> UpdateExchangeRatesAsync(ExchangeRate[] exchangeRates, ExchangeRateProvider provider, EventBus eventBus, CancellationToken cancellationToken)
	{
		var newExchangeRates = await provider(cancellationToken).ConfigureAwait(false);
		if (newExchangeRates.Length != 0 && !newExchangeRates.SequenceEqual(exchangeRates))
		{
			exchangeRates = newExchangeRates;
			eventBus.Publish(new ExchangeRatesChanged(newExchangeRates));
		}

		return exchangeRates;
	}
}
