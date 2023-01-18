using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBitcoin.Protocol;
using ReactiveUI;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Fluent.AppServices.Tor;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public partial class StatusIconViewModel : ObservableObject, IStatusIconViewModel, IDisposable
{
	private readonly TorStatusCheckerWrapper _statusCheckerWrapper;
	[ObservableProperty] private StatusIconState _currentState;
	[ObservableProperty] private TorStatus _torStatus;
	[ObservableProperty] private BackendStatus _backendStatus;
	[ObservableProperty] private RpcStatus? _bitcoinCoreStatus;
	[ObservableProperty] private int _peers;
	[ObservableProperty] private bool _updateAvailable;
	[ObservableProperty] private bool _criticalUpdateAvailable;
	[ObservableProperty] private bool _isReadyToInstall;
	[ObservableProperty] private bool _isConnectionIssueDetected;
	[ObservableProperty] private string? _versionText;
	[ObservableProperty] private ICollection<Issue>? _torIssues;

	public StatusIconViewModel(TorStatusCheckerWrapper statusCheckerWrapper)
	{
		_statusCheckerWrapper = statusCheckerWrapper;
		UseTor = Services.Config.UseTor; // Do not make it dynamic, because if you change this config settings only next time will it activate.
		TorStatus = UseTor ? Services.Synchronizer.TorStatus : TorStatus.TurnedOff;
		UseBitcoinCore = Services.Config.StartLocalBitcoinCoreOnStartup;

		ManualUpdateCommand = new AsyncRelayCommand(() => IoHelpers.OpenBrowserAsync("https://wasabiwallet.io/#download"));
		UpdateCommand = new RelayCommand(() =>
		{
			Services.UpdateManager.DoUpdateOnClose = true;
			AppLifetimeHelper.Shutdown();
		});

		AskMeLaterCommand = new RelayCommand(() => UpdateAvailable = false);

		OpenTorStatusSiteCommand = new AsyncRelayCommand(() => IoHelpers.OpenBrowserAsync("https://status.torproject.org"));

		this.WhenAnyValue(
				x => x.TorStatus,
				x => x.BackendStatus,
				x => x.Peers,
				x => x.BitcoinCoreStatus,
				x => x.UpdateAvailable,
				x => x.CriticalUpdateAvailable,
				x => x.IsConnectionIssueDetected)
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				if (BackendStatus == BackendStatus.Connected)
				{
					IsConnectionIssueDetected = false;
				}

				SetStatusIconState();
			});
	}

	public ICommand OpenTorStatusSiteCommand { get; }

	public ICommand UpdateCommand { get; }
	public ICommand ManualUpdateCommand { get; }

	public ICommand AskMeLaterCommand { get; }

	private CompositeDisposable Disposables { get; } = new();

	private bool UseTor { get; }

	public bool UseBitcoinCore { get; }

	public string BitcoinCoreName => Constants.BuiltinBitcoinNodeName;

	private void SetStatusIconState()
	{
		if (IsConnectionIssueDetected)
		{
			CurrentState = StatusIconState.ConnectionIssueDetected;
			return;
		}

		if (CriticalUpdateAvailable)
		{
			CurrentState = StatusIconState.CriticalUpdateAvailable;
			return;
		}

		if (UpdateAvailable)
		{
			CurrentState = StatusIconState.UpdateAvailable;
			return;
		}

		// The source of the p2p connection comes from if we use Core for it or the network.
		var p2pConnected = UseBitcoinCore ? BitcoinCoreStatus?.Success is true : Peers >= 1;
		var torConnected = !UseTor || TorStatus == TorStatus.Running;

		if (torConnected && BackendStatus == BackendStatus.Connected && p2pConnected)
		{
			CurrentState = StatusIconState.Ready;
			return;
		}

		CurrentState = StatusIconState.Loading;
	}

	public void Initialize()
	{
		var nodes = Services.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes;
		var synchronizer = Services.Synchronizer;
		var rpcMonitor = Services.HostedServices.GetOrDefault<RpcMonitor>();
		var updateChecker = Services.HostedServices.Get<UpdateChecker>();
		var updateManager = Services.UpdateManager;

		BitcoinCoreStatus = rpcMonitor?.RpcStatus ?? RpcStatus.Unresponsive;

		synchronizer.WhenAnyValue(x => x.TorStatus)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(status => TorStatus = UseTor ? status : TorStatus.TurnedOff)
			.DisposeWith(Disposables);

		synchronizer.WhenAnyValue(x => x.BackendStatus)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(status => BackendStatus = status)
			.DisposeWith(Disposables);

		Observable.FromEventPattern<bool>(Services.Synchronizer, nameof(Services.Synchronizer.ResponseArrivedIsGenSocksServFail))
			.Subscribe(_ =>
			{
				if (BackendStatus == BackendStatus.Connected) // TODO: the event invoke must be refactored in Synchronizer
				{
					return;
				}

				IsConnectionIssueDetected = true;
			})
			.DisposeWith(Disposables);

		Peers = TorStatus == TorStatus.NotRunning ? 0 : nodes.Count;
		Observable
			.Merge(Observable.FromEventPattern(nodes, nameof(nodes.Added)).Select(_ => Unit.Default)
			.Merge(Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed)).Select(_ => Unit.Default)
			.Merge(Services.Synchronizer.WhenAnyValue(x => x.TorStatus).Select(_ => Unit.Default))))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => Peers = synchronizer.TorStatus == TorStatus.NotRunning ? 0 : nodes.Count) // Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it seems to the user that peers are connected over clearnet, while they are not.
			.DisposeWith(Disposables);

		if (rpcMonitor is { }) // TODO: Is it possible?
		{
			Observable.FromEventPattern<RpcStatus>(rpcMonitor, nameof(rpcMonitor.RpcStatusChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e => BitcoinCoreStatus = e.EventArgs)
				.DisposeWith(Disposables);
		}

		Observable.FromEventPattern<UpdateStatus>(updateManager, nameof(updateManager.UpdateAvailableToGet))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(e =>
			{
				var updateStatus = e.EventArgs;

				UpdateAvailable = !updateStatus.ClientUpToDate;
				CriticalUpdateAvailable = !updateStatus.BackendCompatible;
				IsReadyToInstall = updateStatus.IsReadyToInstall;

				if (CriticalUpdateAvailable)
				{
					VersionText = $"Critical update required";
				}
				else if (IsReadyToInstall)
				{
					VersionText = $"Version {updateStatus.ClientVersion} is now ready to install";
				}
				else if (UpdateAvailable)
				{
					VersionText = $"Version {updateStatus.ClientVersion} is now available";
				}
			})
			.DisposeWith(Disposables);

		_statusCheckerWrapper.Issues
			.Select(r => r.Where(issue => !issue.Resolved).ToList())
			.ObserveOn(RxApp.MainThreadScheduler)
			.BindTo(this, x => x.TorIssues)
			.DisposeWith(Disposables);
	}

	public void Dispose()
	{
		Disposables.Dispose();
	}
}
