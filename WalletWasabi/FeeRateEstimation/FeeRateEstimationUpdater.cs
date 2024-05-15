using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;

namespace WalletWasabi.FeeRateEstimation;

public class FeeRateEstimationUpdater :  PeriodicRunner
{
	private readonly Func<string> _feeRateProviderGetter;
	private readonly FeeRateProvider _provider;
	public AllFeeEstimate? AllFeeEstimate { get; private set; }
	public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

	public FeeRateEstimationUpdater(TimeSpan period, Func<string> feeRateProviderGetter, EndPoint? socksProxyUri = null)
		: base(period)
	{
		_provider = new FeeRateProvider(socksProxyUri);
		_feeRateProviderGetter = feeRateProviderGetter;
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var newFeeRAteEstimations = await _provider.GetFeeRateEstimationsAsync(_feeRateProviderGetter(), cancellationToken).ConfigureAwait(false);
		if (newFeeRAteEstimations != AllFeeEstimate)
		{
			AllFeeEstimate = newFeeRAteEstimations;
			AllFeeEstimateChanged.SafeInvoke(this, AllFeeEstimate);
		}
	}
}
