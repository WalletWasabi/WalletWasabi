using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin.Protocol;
using ReactiveUI;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public partial class StatusIconViewModel : IStatusIconViewModel, IDisposable
{
	[AutoNotify] private StatusIconState _currentState;
	[AutoNotify] private TorStatus _torStatus;
	[AutoNotify] private BackendStatus _backendStatus;
	[AutoNotify] private RpcStatus? _bitcoinCoreStatus;
	[AutoNotify] private int _peers;
	[AutoNotify] private bool _updateAvailable;
	[AutoNotify] private bool _criticalUpdateAvailable;
	[AutoNotify] private bool _isReadyToInstall;
	[AutoNotify] private bool _isConnectionIssueDetected;
	[AutoNotify] private string? _versionText;
	private readonly ObservableAsPropertyHelper<ICollection<Issue>> _torIssues;

	public StatusIconViewModel(TorStatusCheckerModel statusCheckerWrapper)
	{
		UseTor = Services.PersistentConfig.UseTor; // Do not make it dynamic, because if you change this config settings only next time will it activate.
		TorStatus = UseTor ? Services.Synchronizer.TorStatus : TorStatus.TurnedOff;
		UseBitcoinCore = Services.PersistentConfig.StartLocalBitcoinCoreOnStartup;

		ManualUpdateCommand = ReactiveCommand.CreateFromTask(() => IoHelpers.OpenBrowserAsync("https://wasabiwallet.io/#download"));
		UpdateCommand = ReactiveCommand.Create(() =>
		{
			Services.UpdateManager.DoUpdateOnClose = true;
			AppLifetimeHelper.Shutdown();
		});

		AskMeLaterCommand = ReactiveCommand.Create(() => UpdateAvailable = false);

		OpenTorStatusSiteCommand = ReactiveCommand.CreateFromTask(() => IoHelpers.OpenBrowserAsync("https://status.torproject.org"));

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

		var issues = statusCheckerWrapper.Issues
			.Select(r => r.Where(issue => !issue.Resolved).ToList())
			.ObserveOn(RxApp.MainThreadScheduler)
			.Publish();

		_torIssues = issues
			.ToProperty(this, m => m.TorIssues);

		issues.Connect();
	}

	public ICollection<Issue> TorIssues => _torIssues.Value;
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
			.Merge(Observable.FromEventPattern(nodes, nameof(nodes.Added)).ToSignal()
			.Merge(Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed)).ToSignal()
			.Merge(Services.Synchronizer.WhenAnyValue(x => x.TorStatus).ToSignal())))
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
	}

	public void Dispose()
	{
		Disposables.Dispose();
	}
}
