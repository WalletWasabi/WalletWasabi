using NBitcoin.Protocol;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.BitcoinP2p;
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
}

[AutoInterface]
public partial class HealthMonitor : ReactiveObject
{
	private readonly ObservableAsPropertyHelper<ICollection<Issue>> _torIssues;

	[AutoNotify] private int _priorityFee;
	[AutoNotify] private uint _blockchainTip;
	[AutoNotify] private TorStatus _torStatus;
	[AutoNotify] private IndexerStatus _indexerStatus;
	[AutoNotify] private bool _backendNotCompatible;
	[AutoNotify] private bool _isConnectionIssueDetected;
	[AutoNotify] private bool _isBitcoinCoreIssueDetected;
	[AutoNotify] private bool _isBitcoinCoreSynchronizingOrConnecting;
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
		UseBitcoinRpc = applicationSettings.UseBitcoinRpc;
		CanUseBitcoinRpc = UseBitcoinRpc && !string.IsNullOrWhiteSpace(applicationSettings.BitcoinRpcCredentialString);

		var nodes = Services.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes;

		// Priority Fee
		Services.EventBus.AsObservable<MiningFeeRatesChanged>()
			.Select(e => e.AllFeeEstimate.Estimations.FirstOrDefault(x => x.Key == 2).Value)
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
			.Do(x => IsConnectionIssueDetected = !x.IsIndexerAvailable)
			.Select(x => x.IsIndexerAvailable ? IndexerStatus.Connected : IndexerStatus.NotConnected)
			.BindTo(this, x => x.IndexerStatus)
			.DisposeWith(Disposables);

		// Indexer compatibility
		Services.EventBus.AsObservable<IndexerIncompatibilityDetected>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => true)
			.BindTo(this, x => x.BackendNotCompatible)
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
			.Merge(Services.EventBus.AsObservable<TorConnectionStateChanged>().ToSignal())))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ =>
				  UseTor != TorMode.Disabled && TorStatus == TorStatus.NotRunning ? 0 : nodes.Count) // Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it seems to the user that peers are connected over clearnet, while they are not.
			.BindTo(this, x => x.Peers)
			.DisposeWith(Disposables);

		// Bitcoin Core Status
		Services.EventBus.AsObservable<RpcStatusChanged>()
			.Select(x => x.Status)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x =>
			{
				BitcoinRpcStatus = x;
				IsBitcoinCoreSynchronizingOrConnecting = x.Match(r => !r.Synchronized, _ => false);
				IsBitcoinCoreIssueDetected = !x.IsOk;
			})
			.DisposeWith(Disposables);

		// Is P2P Connected
		// The source of the p2p connection comes from if we use Core for it or the network.
		this.WhenAnyValue(x => x.Peers)
			.Select((s, p) => Peers >= 1)
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
				x => x.BackendNotCompatible,
				x => x.Peers,
				x => x.BitcoinRpcStatus,
				x => x.UpdateAvailable,
				x => x.IsConnectionIssueDetected,
				x => x.IsBitcoinCoreIssueDetected,
				x => x.IsBitcoinCoreSynchronizingOrConnecting,
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
		if (CheckForUpdates && UpdateAvailable)
		{
			return HealthMonitorState.UpdateAvailable;
		}

		if (BackendNotCompatible)
		{
			return HealthMonitorState.BackendNotCompatible;
		}

		if (IsBitcoinCoreIssueDetected)
		{
			return HealthMonitorState.BitcoinCoreIssueDetected;
		}

		if (IsConnectionIssueDetected)
		{
			return HealthMonitorState.ConnectionIssueDetected;
		}

		if (IsBitcoinCoreSynchronizingOrConnecting)
		{
			return HealthMonitorState.BitcoinCoreSynchronizingOrConnecting;
		}

		var torConnected = UseTor == TorMode.Disabled || TorStatus == TorStatus.Running;
		if (torConnected && IndexerStatus == IndexerStatus.Connected && IsP2pConnected)
		{
			return HealthMonitorState.Ready;
		}
		if (CanUseBitcoinRpc && BitcoinRpcStatus.Match(x => x.Synchronized, _ => false))
		{
			return HealthMonitorState.Ready;
		}
		return HealthMonitorState.Loading;
	}
}
