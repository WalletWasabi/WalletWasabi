using System.Net;
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

	public AllFeeEstimate? AllFeeEstimate { get; private set; }
	public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

	public FeeRateEstimationUpdater(TimeSpan period, Func<string> feeRateProviderGetter, EndPoint? socksProxyUri = null)
		: base(period)
	{
		_provider = new FeeRateProvider(socksProxyUri);
		_feeRateProviderGetter = feeRateProviderGetter;
		_userAgentPicker = UserAgent.GenerateUserAgentPicker(socksProxyUri is null);
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newFeeRateEstimations = await _provider.GetFeeRateEstimationsAsync(_feeRateProviderGetter(), _userAgentPicker(), cancellationToken).ConfigureAwait(false);
		if (newFeeRateEstimations != AllFeeEstimate)
		{
			Logger.LogInfo($"Fetched fee rate estimations {_feeRateProviderGetter()}.");

			AllFeeEstimate = newFeeRateEstimations;
			AllFeeEstimateChanged.SafeInvoke(this, AllFeeEstimate);
		}

		await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(120)), cancellationToken).ConfigureAwait(false);
	}
}
