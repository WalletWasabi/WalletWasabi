using System;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Services;

namespace WalletWasabi.Daemon;

public class StatusContainer : IDisposable
{
	public bool IsTorRunning { get; private set; }
	public decimal UsdExchangeRate { get; private set; }
	public FeeRateEstimations? FeeRates { get; private set; }
	public int BestHeight { get; private set; }
	public bool IsIndexerAvailable { get; private set; }

	private readonly IDisposable _torConnectionSubscription;
	private readonly IDisposable _feeRateSubscription;
	private readonly IDisposable _exchangeRateSubscription;
	private readonly IDisposable _bestHeightSubscription;
	private readonly IDisposable _indexerConnectionStatusSubscription;

	public StatusContainer(EventBus eventBus)
	{
		_torConnectionSubscription =
			eventBus.Subscribe<TorConnectionStateChanged>(e => IsTorRunning = e.IsTorRunning);

		_feeRateSubscription =
			eventBus.Subscribe<MiningFeeRatesChanged>(e => FeeRates = e.AllFeeEstimate);

		_exchangeRateSubscription =
			eventBus.Subscribe<ExchangeRateChanged>(e => UsdExchangeRate = e.UsdBtcRate);

		_bestHeightSubscription =
			eventBus.Subscribe<ServerTipHeightChanged>(e => BestHeight = e.Height);

		_indexerConnectionStatusSubscription =
			eventBus.Subscribe<IndexerAvailabilityStateChanged>(e => IsIndexerAvailable = e.IsIndexerAvailable);
	}


	public void Dispose()
	{
		_torConnectionSubscription.Dispose();
		_feeRateSubscription.Dispose();
		_exchangeRateSubscription.Dispose();
		_bestHeightSubscription.Dispose();
		_indexerConnectionStatusSubscription.Dispose();
	}
}
