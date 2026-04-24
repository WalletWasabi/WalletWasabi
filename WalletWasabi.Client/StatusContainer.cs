using System;
using System.Collections.Generic;
using System.IO;
using WalletWasabi.Discoverability;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using WalletWasabi.Services;

namespace WalletWasabi.Daemon;

public class StatusContainer : IDisposable
{
	public bool IsTorRunning { get; private set; }
	public decimal UsdExchangeRate { get; private set; }
	public FeeRateEstimations? FeeRates { get; private set; }
	public uint BestHeight { get; private set; }
	public bool InstallOnClose { get; private set; }
	public string InstallerFilePath { get; private set; } = string.Empty;
	public IReadOnlyList<KnownCoordinator> KnownCoordinators { get; }

	private readonly IDisposable _torConnectionSubscription;
	private readonly IDisposable _feeRateSubscription;
	private readonly IDisposable _exchangeRateSubscription;
	private readonly IDisposable _bestHeightSubscription;
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

		_installerAvailableSubscription =
			eventBus.Subscribe<NewSoftwareVersionInstallerAvailable>(e => InstallerFilePath = e.InstallerPath);

		InstallOnClose = installOnClose;
		_installOnCloseSubscription =
			eventBus.Subscribe<InstallOnClosedPreferenceChanged>(e => InstallOnClose = e.InstallOnClose);

		KnownCoordinators = LoadBundledKnownCoordinators();
	}

	private static IReadOnlyList<KnownCoordinator> LoadBundledKnownCoordinators()
	{
		var path = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "Discoverability", "KnownCoordinators.json");
		if (!File.Exists(path))
		{
			return [];
		}

		var parsed = JsonDecoder.FromString(Decode.Array(Decode.KnownCoordinator))(File.ReadAllText(path));
		return parsed.Match<IReadOnlyList<KnownCoordinator>>(coords => coords, _ => []);
	}

	public void Dispose()
	{
		_torConnectionSubscription.Dispose();
		_feeRateSubscription.Dispose();
		_exchangeRateSubscription.Dispose();
		_bestHeightSubscription.Dispose();
		_installerAvailableSubscription.Dispose();
		_installOnCloseSubscription.Dispose();
	}
}
