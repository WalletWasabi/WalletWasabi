using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WebClients;

namespace WalletWasabi.Wallets.Exchange;

public static class ExchangeRateUpdater
{
	public static readonly string ServiceName = "ExchangeFeeRateUpdater";
	public record UpdateMessage;

	public static Func<UpdateMessage, decimal, CancellationToken, Task<decimal>> CreateExchangeRateUpdater(
		Func<string> exchangeRateProviderGetter, IHttpClientFactory httpClientFactory, EventBus eventBus) =>
		(_, usdExchangeRate, cancellationToken) => UpdateExchangeRateAsync(usdExchangeRate, new ExchangeRateProvider(httpClientFactory),
			exchangeRateProviderGetter, UserAgent.GenerateUserAgentPicker(false),
			eventBus, cancellationToken);

	private static async Task<decimal> UpdateExchangeRateAsync(decimal usdExchangeRate, ExchangeRateProvider provider, Func<string> exchangeRateProviderGetter, UserAgentPicker userAgentPicker, EventBus eventBus, CancellationToken cancellationToken)
	{
		var newExchangeRate = await provider.GetExchangeRateAsync(exchangeRateProviderGetter(), userAgentPicker(), cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != usdExchangeRate && newExchangeRate.Rate > 0m)
		{
			usdExchangeRate = newExchangeRate.Rate;
			eventBus.Publish(new ExchangeRateChanged(newExchangeRate.Rate));
			Logger.LogInfo($"Fetched exchange rate from {exchangeRateProviderGetter()}: {newExchangeRate.Rate}.");
		}

		return usdExchangeRate;
	}

	public static void UpdateExchangeRate() => Workers.Tell(ServiceName, new UpdateMessage());
}
