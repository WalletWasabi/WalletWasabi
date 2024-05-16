using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WebClients;

namespace WalletWasabi.ExchangeRate;

public class ExchangeRateUpdater : PeriodicRunner, INotifyPropertyChanged
{
	private readonly Func<string> _exchangeRateProviderGetter;
	private readonly ExchangeRateProvider _provider;
	private readonly UserAgentPicker _userAgentPicker;
	public decimal UsdExchangeRate { get; private set; }

	public event PropertyChangedEventHandler? PropertyChanged;

	public ExchangeRateUpdater(TimeSpan period, Func<string> exchangeRateProviderGetter, EndPoint? socksProxyUri = null)
		: base(period)
	{
		_provider = new ExchangeRateProvider(socksProxyUri);
		_exchangeRateProviderGetter = exchangeRateProviderGetter;
		_userAgentPicker = UserAgent.GenerateUserAgentPicker(socksProxyUri is null);
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newExchangeRate = await _provider.GetExchangeRateAsync(_exchangeRateProviderGetter(), _userAgentPicker(), cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != UsdExchangeRate && newExchangeRate.Rate > 0m)
		{
			UsdExchangeRate = newExchangeRate.Rate;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsdExchangeRate)));
		}
	}
}
