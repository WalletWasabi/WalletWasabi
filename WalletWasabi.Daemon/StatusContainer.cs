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
	public bool InstallOnClose { get; private set; }
	public string InstallerFilePath { get; private set; } = string.Empty;

	private readonly IDisposable _torConnectionSubscription;
	private readonly IDisposable _feeRateSubscription;
	private readonly IDisposable _exchangeRateSubscription;
	private readonly IDisposable _bestHeightSubscription;
	private readonly IDisposable _indexerConnectionStatusSubscription;
	private readonly IDisposable _installOnCloseSubscription;
	private readonly IDisposable _installerAvailableSubscription;

	public StatusContainer(EventBus eventBus, bool installOnClose = false)
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

		_installerAvailableSubscription =
			eventBus.Subscribe<NewSoftwareVersionInstallerAvailable>(e => InstallerFilePath = e.InstallerPath);

		InstallOnClose = installOnClose;
		_installOnCloseSubscription =
			eventBus.Subscribe<InstallOnClosedPreferenceChanged>(e => InstallOnClose = e.InstallOnClose);
	}


	public void Dispose()
	{
		_torConnectionSubscription.Dispose();
		_feeRateSubscription.Dispose();
		_exchangeRateSubscription.Dispose();
		_bestHeightSubscription.Dispose();
		_indexerConnectionStatusSubscription.Dispose();
		_installerAvailableSubscription.Dispose();
		_installOnCloseSubscription.Dispose();
	}
}
