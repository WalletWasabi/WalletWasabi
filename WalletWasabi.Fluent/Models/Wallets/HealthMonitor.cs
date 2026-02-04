using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial interface IHealthMonitor : IDisposable
{
	decimal PriorityFee { get; set; }

	uint BlockchainTip { get; set; }

	TorStatus TorStatus { get; set; }

	IndexerStatus IndexerStatus { get; set; }

	bool IncompatibleIndexer { get; set; }

	Result<ConnectedRpcStatus, string> BitcoinRpcStatus { get; set; }

	int Peers { get; set; }

	bool IsP2pConnected { get; set; }

	HealthMonitorState State { get; set; }

	bool UpdateAvailable { get; set; }

	bool IsReadyToInstall { get; set; }

	bool CheckForUpdates { get; set; }

	Version? ClientVersion { get; set; }

	bool CanUseBitcoinRpc { get; set; }

	ICollection<Issue> TorIssues { get; }

	TorMode UseTor { get; }
}

[AutoInterface]
public partial class HealthMonitor : ReactiveObject, IHealthMonitor
{
	private readonly ObservableAsPropertyHelper<ICollection<Issue>> _torIssues;

	[AutoNotify] private decimal _priorityFee;
	[AutoNotify] private uint _blockchainTip;
	[AutoNotify] private TorStatus _torStatus;
	[AutoNotify] private IndexerStatus _indexerStatus;
	[AutoNotify] private bool _incompatibleIndexer;
	[AutoNotify] private Result<ConnectedRpcStatus, string> _bitcoinRpcStatus;
	[AutoNotify] private int _peers;
	[AutoNotify] private bool _isP2pConnected;
	[AutoNotify] private HealthMonitorState _state;
	[AutoNotify] private bool _updateAvailable;
	[AutoNotify] private bool _isReadyToInstall;
	[AutoNotify] private bool _checkForUpdates = true;
	[AutoNotify] private Version? _clientVersion;
	[AutoNotify] private bool _canUseBitcoinRpc;

	public HealthMonitor(IApplicationSettings applicationSettings, ITorStatusCheckerModel torStatusChecker)
	{
		// Do not make it dynamic, because if you change this config settings only next time will it activate.
		UseTor = Services.Config.UseTor;
		TorStatus = UseTor == TorMode.Disabled ? TorStatus.TurnedOff : TorStatus.NotRunning;
		CanUseBitcoinRpc = applicationSettings.UseBitcoinRpc && !string.IsNullOrWhiteSpace(applicationSettings.BitcoinRpcCredentialString);
		_bitcoinRpcStatus = Result<ConnectedRpcStatus, string>.Fail("");

		// Priority Fee
		Services.EventBus.AsObservable<MiningFeeRatesChanged>()
			.Select(e => e.AllFeeEstimate.Estimations.FirstOrDefault(x => x.Key == 2).Value)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(priorityFee => PriorityFee = priorityFee.SatoshiPerByte);

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
		Services.EventBus.AsObservable<TorConnectionStateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(status => (UseTor, status.IsTorRunning) switch
			{
				(TorMode.Disabled, _) => TorStatus.TurnedOff,
				(_, true) => TorStatus.Running,
				(_, false) => TorStatus.NotRunning
			})
			.BindTo(this, x => x.TorStatus)
			.DisposeWith(Disposables);

		// Indexer Status
		Services.EventBus.AsObservable<IndexerAvailabilityStateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.IsIndexerAvailable ? IndexerStatus.Connected : IndexerStatus.NotConnected)
			.BindTo(this, x => x.IndexerStatus)
			.DisposeWith(Disposables);

		// Indexer compatibility
		Services.EventBus.AsObservable<IndexerIncompatibilityDetected>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => true)
			.BindTo(this, x => x.IncompatibleIndexer)
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

		var nodesCount = 0;
		var peersObservable = Services.EventBus.AsObservable<BitcoinPeersChanged>();
		peersObservable.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => nodesCount = x.NodesCount)
			.DisposeWith(Disposables);

		// Peers
		Observable.Merge( peersObservable.ToSignal()
			.Merge(Services.EventBus.AsObservable<TorConnectionStateChanged>().ToSignal()))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ =>
				  UseTor != TorMode.Disabled && TorStatus == TorStatus.NotRunning ? 0 : nodesCount) // Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it seems to the user that peers are connected over clearnet, while they are not.
			.BindTo(this, x => x.Peers)
			.DisposeWith(Disposables);

		// Bitcoin Core Status
		Services.EventBus.AsObservable<RpcStatusChanged>()
			.Select(x => x.Status)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => BitcoinRpcStatus = x)
			.DisposeWith(Disposables);

		// Is P2P Connected
		// The source of the p2p connection comes from if we use Core for it or the network.
		this.WhenAnyValue(x => x.Peers)
			.Select(peerCount => peerCount > 0)
			.BindTo(this, x => x.IsP2pConnected)
			.DisposeWith(Disposables);

		// Update Available
		Services.EventBus.AsObservable<NewSoftwareVersionAvailable>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(e =>
			{
				var updateStatus = e.UpdateStatus;

				UpdateAvailable = !updateStatus.ClientUpToDate;
				IsReadyToInstall = updateStatus.IsReadyToInstall;
				ClientVersion = updateStatus.ClientVersion;
			})
			.DisposeWith(Disposables);

		// State
		this.WhenAnyValue(
				x => x.TorStatus,
				x => x.IndexerStatus,
				x => x.IncompatibleIndexer,
				x => x.Peers,
				x => x.BitcoinRpcStatus,
				x => x.UpdateAvailable,
				x => x.CheckForUpdates)
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => GetState())
			.BindTo(this, x => x.State)
			.DisposeWith(Disposables);
	}

	public ICollection<Issue> TorIssues => _torIssues.Value;

	public TorMode UseTor { get; }

	private CompositeDisposable Disposables { get; } = new();

	public void Dispose()
	{
		Disposables.Dispose();
	}

	private HealthMonitorState GetState()
	{
		if (CheckForUpdates && UpdateAvailable)
		{
			return HealthMonitorState.UpdateAvailable;
		}

		if (IncompatibleIndexer)
		{
			return HealthMonitorState.IncompatibleIndexer;
		}

		var torConnected = UseTor == TorMode.Disabled || TorStatus == TorStatus.Running;
		if (torConnected && IndexerStatus == IndexerStatus.Connected && IsP2pConnected)
		{
			return HealthMonitorState.Ready;
		}

		if (CanUseBitcoinRpc)
		{
			return _bitcoinRpcStatus.Match(
				r => r.Synchronized
					? HealthMonitorState.Ready
					: HealthMonitorState.BitcoinRpcSynchronizing,
				_ =>
					HealthMonitorState.BitcoinRpcIssueDetected);
		}

		if (IndexerStatus is IndexerStatus.NotConnected)
		{
			return HealthMonitorState.IndexerConnectionIssueDetected;
		}

		return HealthMonitorState.Loading;
	}
}
