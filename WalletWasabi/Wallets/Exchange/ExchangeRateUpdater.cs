using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Services;

namespace WalletWasabi.Wallets.Exchange;

public class ExchangeRateUpdater : PeriodicRunner
{
	private readonly EventBus _eventBus;
	private readonly ExchangeRateProvider _provider;
	public decimal UsdExchangeRate { get; private set; }

	public ExchangeRateUpdater(TimeSpan period, ExchangeRateProvider exchangeRateProvider, EventBus eventBus)
		: base(period)
	{
		_provider = exchangeRateProvider;
		_eventBus = eventBus;
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newExchangeRate = await _provider(cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != UsdExchangeRate && newExchangeRate.Rate > 0m)
		{
			UsdExchangeRate = newExchangeRate.Rate;
			_eventBus.Publish(new ExchangeRateChanged(newExchangeRate.Rate));
		}

		await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(120)), cancellationToken).ConfigureAwait(false);
	}
}
