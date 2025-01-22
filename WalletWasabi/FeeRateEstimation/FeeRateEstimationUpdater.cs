using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WebClients;

namespace WalletWasabi.FeeRateEstimation;

public class FeeRateEstimationUpdater :  PeriodicRunner
{
	private readonly Func<string> _feeRateProviderGetter;
	private readonly FeeRateProvider _provider;
	private readonly UserAgentPicker _userAgentPicker;

	public FeeRateEstimations? FeeEstimates { get; private set; }
	public event EventHandler<FeeRateEstimations>? FeeEstimationsChanged;

	public FeeRateEstimationUpdater(TimeSpan period, Func<string> feeRateProviderGetter, IHttpClientFactory httpClientFactory)
		: base(period)
	{
		_provider = new FeeRateProvider(httpClientFactory);
		_feeRateProviderGetter = feeRateProviderGetter;
		_userAgentPicker = UserAgent.GenerateUserAgentPicker(false);
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newFeeRateEstimations = await _provider.GetFeeRateEstimationsAsync(_feeRateProviderGetter(), _userAgentPicker(), cancellationToken).ConfigureAwait(false);
		if (newFeeRateEstimations != FeeEstimates)
		{
			Logger.LogInfo($"Fetched fee rate estimations {_feeRateProviderGetter()}.");

			FeeEstimates = newFeeRateEstimations;
			FeeEstimationsChanged.SafeInvoke(this, FeeEstimates);
		}

		await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(120)), cancellationToken).ConfigureAwait(false);
	}
}
