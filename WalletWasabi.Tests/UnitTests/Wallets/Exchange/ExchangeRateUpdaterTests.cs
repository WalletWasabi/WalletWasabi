using System.Threading.Tasks;
using WalletWasabi.Client.Wallets.Exchange;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets.Exchange;

/// <summary>
/// Tests for <see cref="ExchangeRateUpdater"/>.
/// </summary>
public class ExchangeRateUpdaterTests
{
	[Fact]
	public async Task PublishingAsync()
	{
		ExchangeRate[] previousRates = [];
		ExchangeRate[] apiRatesResponse = [new("USD", 75_000)];

		var eventBus = new EventBus();
		var published = 0;
		using var subscription = eventBus.Subscribe<ExchangeRatesChanged>(_ => published++);
		var updaterMsgHandler = ExchangeRateUpdater.CreateExchangeRateUpdater(_ => Task.FromResult(apiRatesResponse), eventBus);

		// We have no exchange rates and new exchange rate for USD comes. Published.
		{
			var newRates = await updaterMsgHandler(UpdateMessage.Instance, previousRates, TestContext.Current.CancellationToken);
			Assert.Equal(1, published);
			Assert.Equal(apiRatesResponse, newRates);

			previousRates = newRates;
		}

		// BTC/USD exchange rate changes. Published.
		{
			apiRatesResponse = [new("USD", 75_500)];

			var newRates = await updaterMsgHandler(UpdateMessage.Instance, previousRates, TestContext.Current.CancellationToken);
			Assert.Equal(2, published);
			Assert.Equal(apiRatesResponse, newRates);

			previousRates = newRates;
		}

		// No exchange rates returned by the provider. Nothing is published.
		{
			apiRatesResponse = [];

			var newRates = await updaterMsgHandler(UpdateMessage.Instance, previousRates, TestContext.Current.CancellationToken);
			Assert.Equal(2, published);
			Assert.Equal(new ExchangeRate("USD", 75_500), newRates.ForCurrency("USD"));
		}
	}
}
