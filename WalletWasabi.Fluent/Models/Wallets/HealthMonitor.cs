using NBitcoin.Protocol;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial interface IHealthMonitor : IDisposable
{
}

[AutoInterface]
public partial class HealthMonitor : ReactiveObject
{
	private readonly ObservableAsPropertyHelper<ICollection<Issue>> _torIssues;

	[AutoNotify] private int _priorityFee;
	[AutoNotify] private uint _blockchainTip;
	[AutoNotify] private TorStatus _torStatus;
	[AutoNotify] private BackendStatus _backendStatus;
	[AutoNotify] private bool _backendNotCompatible;
	[AutoNotify] private bool _isConnectionIssueDetected;
	[AutoNotify] private RpcStatus? _bitcoinRpcStatus;
	[AutoNotify] private int _peers;
	[AutoNotify] private bool _isP2pConnected;
	[AutoNotify] private HealthMonitorState _state;
	[AutoNotify] private bool _updateAvailable;
	[AutoNotify] private bool _isReadyToInstall;
	[AutoNotify] private bool _checkForUpdates = true;
	[AutoNotify] private Version? _clientVersion;

	public HealthMonitor(IApplicationSettings applicationSettings, ITorStatusCheckerModel torStatusChecker)
	{
		// Do not make it dynamic, because if you change this config settings only next time will it activate.
		UseTor = applicationSettings.UseTor;
		UseBitcoinRpc = applicationSettings.UseBitcoinRpc;

		var nodes = Services.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes;
		var synchronizer = Services.HostedServices.Get<WasabiSynchronizer>();

		// Priority Fee
		var feeRateEstimationUpdater = Services.HostedServices.Get<FeeRateEstimationUpdater>();
		Observable
			.FromEventPattern(feeRateEstimationUpdater, nameof(feeRateEstimationUpdater.FeeEstimationsRefreshed))
			.Select(value =>
			{
				return ((FeeRateEstimations)value.EventArgs).Estimations.FirstOrDefault(x => x.Key == 2).Value;
			})
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(priorityFee => PriorityFee = priorityFee);

		// Blockchain Tip
		var smartHeaderChain = Services.BitcoinStore.SmartHeaderChain;
		Observable
			.FromEventPattern(smartHeaderChain, nameof(smartHeaderChain.TipHeightUpdated))
			.Select(value => value.EventArgs)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(blockchainTip =>
			{
				BlockchainTip = (uint)blockchainTip;
			});

		// Tor Status
		synchronizer.WhenAnyValue(x => x.TorStatus)
							 .ObserveOn(RxApp.MainThreadScheduler)
							 .Select(status => UseTor != TorMode.Disabled ? status : TorStatus.TurnedOff)
							 .BindTo(this, x => x.TorStatus)
							 .DisposeWith(Disposables);

		// Backend Status
		synchronizer.WhenAnyValue(x => x.BackendStatus)
							 .ObserveOn(RxApp.MainThreadScheduler)
							 .BindTo(this, x => x.BackendStatus)
							 .DisposeWith(Disposables);

		// Backend compatibility
		synchronizer.WhenAnyValue(x => x.BackendNotCompatible)
			.ObserveOn(RxApp.MainThreadScheduler)
			.BindTo(this, x => x.BackendNotCompatible)
			.DisposeWith(Disposables);

		// Backend Connection Issues flag
		Observable.FromEventPattern<bool>(synchronizer, nameof(synchronizer.SynchronizeRequestFinished))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.EventArgs)
			.Do(isBackendConnected => IsConnectionIssueDetected = !isBackendConnected)
			.Subscribe()
			.DisposeWith(Disposables);

		// Tor Issues
		var issues =
			torStatusChecker.Issues
							.Select(r => r.Where(issue => !issue.Resolved).ToList())
							.ObserveOn(RxApp.MainThreadScheduler)
							.Publish();

		_torIssues = issues.ToProperty(this, m => m.TorIssues);

		issues.Connect()
			  .DisposeWith(Disposables);

		// Peers
		Observable.Merge(Observable.FromEventPattern(nodes, nameof(nodes.Added)).ToSignal()
				  .Merge(Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed)).ToSignal()
				  .Merge(synchronizer.WhenAnyValue(x => x.TorStatus).ToSignal())))
				  .ObserveOn(RxApp.MainThreadScheduler)
				  .Select(_ => synchronizer.TorStatus == TorStatus.NotRunning ? 0 : nodes.Count) // Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it seems to the user that peers are connected over clearnet, while they are not.
				  .BindTo(this, x => x.Peers)
				  .DisposeWith(Disposables);

		// Bitcoin Core Status
		if (UseBitcoinRpc)
		{
			Task.Run(WaitForRpcMonitorAsync);
		}

		// Is P2P Connected
		// The source of the p2p connection comes from if we use Core for it or the network.
		this.WhenAnyValue(x => x.Peers)
			.Select((s, p) => Peers >= 1)
			.BindTo(this, x => x.IsP2pConnected)
			.DisposeWith(Disposables);

		// Update Available
		if (Services.UpdateManager is { })
		{
			Observable.FromEventPattern<UpdateManager.UpdateStatus>(Services.UpdateManager, nameof(Services.UpdateManager.UpdateAvailableToGet))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e =>
				{
					var updateStatus = e.EventArgs;

					UpdateAvailable = !updateStatus.ClientUpToDate;
					IsReadyToInstall = updateStatus.IsReadyToInstall;
					ClientVersion = updateStatus.ClientVersion;
				})
				.DisposeWith(Disposables);
		}

		// State
		this.WhenAnyValue(
				x => x.TorStatus,
				x => x.BackendStatus,
				x => x.BackendNotCompatible,
				x => x.Peers,
				x => x.BitcoinRpcStatus,
				x => x.UpdateAvailable,
				x => x.IsConnectionIssueDetected,
				x => x.CheckForUpdates)
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => GetState())
			.BindTo(this, x => x.State)
			.DisposeWith(Disposables);
	}

	public ICollection<Issue> TorIssues => _torIssues.Value;

	public TorMode UseTor { get; }
	public bool UseBitcoinRpc { get; }

	private CompositeDisposable Disposables { get; } = new();

	public void Dispose()
	{
		Disposables.Dispose();
	}

	private HealthMonitorState GetState()
	{
		if (IsConnectionIssueDetected)
		{
			return HealthMonitorState.ConnectionIssueDetected;
		}

		if (BackendNotCompatible)
		{
			return HealthMonitorState.BackendNotCompatible;
		}

		if (CheckForUpdates && UpdateAvailable)
		{
			return HealthMonitorState.UpdateAvailable;
		}

		var torConnected = UseTor == TorMode.Disabled || TorStatus == TorStatus.Running;
		if (torConnected && BackendStatus == BackendStatus.Connected && IsP2pConnected)
		{
			return HealthMonitorState.Ready;
		}

		return HealthMonitorState.Loading;
	}

	/// <summary>
	/// Loops until the RpcMonitor Service is online and then binds the BitcoinCoreStatus property
	/// </summary>
	private async Task WaitForRpcMonitorAsync()
	{
		while (!Disposables.IsDisposed)
		{
			var rpcMonitor = Services.HostedServices.GetOrDefault<RpcMonitor>();
			if (rpcMonitor is { })
			{
				Observable.FromEventPattern<RpcStatus>(rpcMonitor, nameof(rpcMonitor.RpcStatusChanged))
						  .ObserveOn(RxApp.MainThreadScheduler)
						  .Select(x => x.EventArgs)
						  .BindTo(this, x => x.BitcoinRpcStatus)
						  .DisposeWith(Disposables);
				BitcoinRpcStatus = rpcMonitor.RpcStatus;
				return;
			}

			await Task.Delay(TimeSpan.FromSeconds(1));
		}
	}
}
