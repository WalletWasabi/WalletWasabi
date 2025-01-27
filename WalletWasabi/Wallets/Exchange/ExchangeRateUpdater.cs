using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.WebClients;

namespace WalletWasabi.Wallets.Exchange;

public class ExchangeRateUpdater : PeriodicRunner, INotifyPropertyChanged
{
	private readonly Func<string> _exchangeRateProviderGetter;
	private readonly ExchangeRateProvider _provider;
	private readonly UserAgentPicker _userAgentPicker;
	public decimal UsdExchangeRate { get; private set; }

	public event PropertyChangedEventHandler? PropertyChanged;

	public ExchangeRateUpdater(TimeSpan period, Func<string> exchangeRateProviderGetter, IHttpClientFactory httpClientFactory)
		: base(period)
	{
		_provider = new ExchangeRateProvider(httpClientFactory);
		_exchangeRateProviderGetter = exchangeRateProviderGetter;
		_userAgentPicker = UserAgent.GenerateUserAgentPicker(false);
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newExchangeRate = await _provider.GetExchangeRateAsync(_exchangeRateProviderGetter(), _userAgentPicker(), cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != UsdExchangeRate && newExchangeRate.Rate > 0m)
		{
			UsdExchangeRate = newExchangeRate.Rate;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsdExchangeRate)));
			Logger.LogInfo($"Fetched exchange rate from {_exchangeRateProviderGetter()}: {newExchangeRate.Rate}.");
		}

		await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(120)), cancellationToken).ConfigureAwait(false);
	}
}
