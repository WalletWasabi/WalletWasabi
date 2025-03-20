using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WebClients;

namespace WalletWasabi.Wallets.Exchange;

public class ExchangeRateUpdater : PeriodicRunner
{
	private readonly Func<string> _exchangeRateProviderGetter;
	private readonly EventBus _eventBus;
	private readonly ExchangeRateProvider _provider;
	private readonly UserAgentPicker _userAgentPicker;
	public decimal UsdExchangeRate { get; private set; }

	public ExchangeRateUpdater(TimeSpan period, Func<string> exchangeRateProviderGetter,
		IHttpClientFactory httpClientFactory, EventBus eventBus)
		: base(period)
	{
		_provider = new ExchangeRateProvider(httpClientFactory);
		_exchangeRateProviderGetter = exchangeRateProviderGetter;
		_userAgentPicker = UserAgent.GenerateUserAgentPicker(false);
		_eventBus = eventBus;
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newExchangeRate = await _provider.GetExchangeRateAsync(_exchangeRateProviderGetter(), _userAgentPicker(), cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != UsdExchangeRate && newExchangeRate.Rate > 0m)
		{
			UsdExchangeRate = newExchangeRate.Rate;
			_eventBus.Publish(new ExchangeRateChanged(newExchangeRate.Rate));
			Logger.LogInfo($"Fetched exchange rate from {_exchangeRateProviderGetter()}: {newExchangeRate.Rate}.");
		}

		await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(120)), cancellationToken).ConfigureAwait(false);
	}
}
