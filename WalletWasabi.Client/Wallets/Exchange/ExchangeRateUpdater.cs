using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.Client.Wallets.Exchange;

public record UpdateMessage
{
	public static UpdateMessage Instance { get; } = new();
}

public static class ExchangeRateUpdater
{
	public static readonly string ServiceName = "ExchangeRateUpdater";

	public static MessageHandler<UpdateMessage, ExchangeRate[]> CreateExchangeRateUpdater(
		ExchangeRateProvider provider, EventBus eventBus) =>
		(_, exchangeRates, cancellationToken) => UpdateExchangeRatesAsync(exchangeRates, provider, eventBus, cancellationToken);

	private static async Task<ExchangeRate[]> UpdateExchangeRatesAsync(
		ExchangeRate[] exchangeRates,
		ExchangeRateProvider provider,
		EventBus eventBus,
		CancellationToken cancellationToken)
	{
		var newExchangeRates = await provider(cancellationToken).ConfigureAwait(false);
		if (newExchangeRates.Length != 0 && !newExchangeRates.SequenceEqual(exchangeRates))
		{
			// Preserve the old exchange rates if the new ones are missing some currencies. Thus some values can be outdated but that's acceptable here.
			exchangeRates = newExchangeRates.UnionBy(exchangeRates, x => x.Ticker).ToArray();
			eventBus.Publish(new ExchangeRatesChanged(exchangeRates));
		}

		return exchangeRates;
	}
}
