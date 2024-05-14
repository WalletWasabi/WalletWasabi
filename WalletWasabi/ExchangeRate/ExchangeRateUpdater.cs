using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.ExchangeRate;

public class ExchangeRateUpdater : PeriodicRunner, INotifyPropertyChanged
{
	private readonly ExchangeRateProvider _provider = new();
	private string _exchangeRateProviderName = "Bitstamp";
	public decimal UsdExchangeRate { get; private set; }

	public event PropertyChangedEventHandler? PropertyChanged;

	public ExchangeRateUpdater(TimeSpan period)
		: base(period)
	{
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newExchangeRate = await _provider.GetExchangeRateAsync(_exchangeRateProviderName, cancellationToken).ConfigureAwait(false);
		if (newExchangeRate.Rate != UsdExchangeRate && newExchangeRate.Rate > 0m)
		{
			UsdExchangeRate = newExchangeRate.Rate;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsdExchangeRate)));
		}
	}
}
