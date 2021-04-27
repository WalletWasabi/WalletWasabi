using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using NBitcoin.Protocol;
using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Helpers;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Gui.ViewModels
{
	public class StatusBarViewModel : ViewModelBase
	{
		private RpcStatus _bitcoinCoreStatus;
		private UpdateStatus _updateStatus;
		private bool _updateAvailable;
		private bool _criticalUpdateAvailable;

		private bool _useBitcoinCore;
		private BackendStatus _backend;
		private TorStatus _tor;
		private int _peers;
		private ObservableAsPropertyHelper<int> _filtersLeft;
		private string _exchangeRate;
		private bool _isExchangeRateAvailable;
		private ObservableAsPropertyHelper<string> _status;
		private bool _downloadingBlock;

		private volatile bool _disposedValue = false; // To detect redundant calls

		public StatusBarViewModel(string dataDir, Network network, Config config, HostedServices hostedServices, SmartHeaderChain smartHeaderChain, WasabiSynchronizer synchronizer)
		{
			DataDir = dataDir;
			Network = network;
			Config = config;
			HostedServices = hostedServices;
			SmartHeaderChain = smartHeaderChain;
			Synchronizer = synchronizer;
			Backend = BackendStatus.NotConnected;
			UseTor = false;
			Tor = TorStatus.NotRunning;
			Peers = 0;
			_exchangeRate = "";
			IsExchangeRateAvailable = false;
			ActiveStatuses = new StatusSet();
		}

		private CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private NodesCollection Nodes { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }

		private bool UseTor { get; set; }

		private StatusSet ActiveStatuses { get; }

		public ReactiveCommand<Unit, Unit> UpdateCommand { get; set; }

		public string DataDir { get; }

		[SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Network affects status bar background color.")]
		private Network Network { get; }

		private Config Config { get; }

		private HostedServices HostedServices { get; }

		private SmartHeaderChain SmartHeaderChain { get; }

		public bool UseBitcoinCore
		{
			get => _useBitcoinCore;
			set => this.RaiseAndSetIfChanged(ref _useBitcoinCore, value);
		}

		public BackendStatus Backend
		{
			get => _backend;
			set => this.RaiseAndSetIfChanged(ref _backend, value);
		}

		public TorStatus Tor
		{
			get => _tor;
			set => this.RaiseAndSetIfChanged(ref _tor, value);
		}

		public int Peers
		{
			get => _peers;
			set => this.RaiseAndSetIfChanged(ref _peers, value);
		}

		public int FiltersLeft => _filtersLeft?.Value ?? 0;

		public RpcStatus BitcoinCoreStatus
		{
			get => _bitcoinCoreStatus;
			set => this.RaiseAndSetIfChanged(ref _bitcoinCoreStatus, value);
		}

		public UpdateStatus UpdateStatus
		{
			get => _updateStatus;
			set => this.RaiseAndSetIfChanged(ref _updateStatus, value);
		}

		public bool UpdateAvailable
		{
			get => _updateAvailable;
			set => this.RaiseAndSetIfChanged(ref _updateAvailable, value);
		}

		public bool CriticalUpdateAvailable
		{
			get => _criticalUpdateAvailable;
			set => this.RaiseAndSetIfChanged(ref _criticalUpdateAvailable, value);
		}

		public string ExchangeRate
		{
			get => _exchangeRate;
			set => this.RaiseAndSetIfChanged(ref _exchangeRate, value);
		}

		public bool IsExchangeRateAvailable
		{
			get => _isExchangeRateAvailable;
			set => this.RaiseAndSetIfChanged(ref _isExchangeRateAvailable, value);
		}

		public string Status => _status?.Value ?? "Loading...";

		public bool DownloadingBlock
		{
			get => _downloadingBlock;
			set => this.RaiseAndSetIfChanged(ref _downloadingBlock, value);
		}

		public void Initialize(NodesCollection nodes)
		{
			Nodes = nodes;
			UseTor = Config.UseTor; // Do not make it dynamic, because if you change this config settings only next time will it activate.
			UseBitcoinCore = Config.StartLocalBitcoinCoreOnStartup;

			var updateChecker = HostedServices.Get<UpdateChecker>();
			UpdateStatus = updateChecker.UpdateStatus;

			var rpcMonitor = HostedServices.GetOrDefault<RpcMonitor>();
			BitcoinCoreStatus = rpcMonitor?.RpcStatus ?? RpcStatus.Unresponsive;

			_status = ActiveStatuses.WhenAnyValue(x => x.CurrentStatus)
				.Select(x => x.ToString())
				.ObserveOn(RxApp.MainThreadScheduler)
				.ToProperty(this, x => x.Status)
				.DisposeWith(Disposables);

			Observable
				.Merge(Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Added)).Select(x => true)
				.Merge(Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed)).Select(x => true)
				.Merge(Synchronizer.WhenAnyValue(x => x.TorStatus).Select(x => true))))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => Peers = Synchronizer.TorStatus == TorStatus.NotRunning ? 0 : Nodes.Count) // Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it seems to the user that peers are connected over clearnet, while they are not.
				.DisposeWith(Disposables);

			Peers = Tor == TorStatus.NotRunning ? 0 : Nodes.Count;

			Observable.FromEventPattern<bool>(typeof(P2pBlockProvider), nameof(P2pBlockProvider.DownloadingBlockChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => DownloadingBlock = x.EventArgs)
				.DisposeWith(Disposables);

			IDisposable? walletCheckingInterval = null;
			Observable.FromEventPattern<bool>(typeof(Wallet), nameof(Wallet.InitializingChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.EventArgs)
					{
						TryAddStatus(StatusType.WalletLoading);

						if (walletCheckingInterval is null)
						{
							var wallet = x.Sender as Wallet;
							walletCheckingInterval = Observable.Interval(TimeSpan.FromSeconds(1))
								.ObserveOn(RxApp.MainThreadScheduler)
								.Subscribe(_ =>
								{
									if (wallet is { })
									{
										var segwitActivationHeight = SmartHeader.GetStartingHeader(wallet.Network).Height;
										if (wallet.LastProcessedFilter?.Header?.Height is uint lastProcessedFilterHeight
											&& lastProcessedFilterHeight > segwitActivationHeight
											&& SmartHeaderChain.TipHeight is uint tipHeight
											&& tipHeight > segwitActivationHeight)
										{
											var allFilters = tipHeight - segwitActivationHeight;
											var processedFilters = lastProcessedFilterHeight - segwitActivationHeight;
											var perc = allFilters == 0
												? 100
												: ((decimal)processedFilters / allFilters * 100);
											TryAddStatus(StatusType.WalletProcessingFilters, (ushort)perc);
										}

										var txProcessor = wallet.TransactionProcessor;
										if (txProcessor is { })
										{
											var txCount = txProcessor.QueuedTxCount;
											var perc = txCount == 0
												? 100
												: ((decimal)txProcessor.QueuedProcessedTxCount / txCount * 100);
											TryAddStatus(StatusType.WalletProcessingTransactions, (ushort)perc);
										}
									}
								})
								.DisposeWith(Disposables);
						}
					}
					else
					{
						walletCheckingInterval?.Dispose();
						walletCheckingInterval = null;

						TryRemoveStatus(StatusType.WalletLoading, StatusType.WalletProcessingFilters, StatusType.WalletProcessingTransactions);
					}
				})
				.DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.TorStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(status => Tor = UseTor ? status : TorStatus.TurnedOff)
				.DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.BackendStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => Backend = Synchronizer.BackendStatus)
				.DisposeWith(Disposables);

			_filtersLeft = SmartHeaderChain.WhenAnyValue(x => x.HashesLeft)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.ToProperty(this, x => x.FiltersLeft)
				.DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(usd => ExchangeRate = $"${(long)usd}")
				.DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.Select(x => x != default)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsExchangeRateAvailable = x)
				.DisposeWith(Disposables);

			if (rpcMonitor is { })
			{
				Observable.FromEventPattern<RpcStatus>(rpcMonitor, nameof(rpcMonitor.RpcStatusChanged))
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(e => BitcoinCoreStatus = e.EventArgs)
					.DisposeWith(Disposables);
			}

			Observable.FromEventPattern<UpdateStatus>(updateChecker, nameof(updateChecker.UpdateStatusChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e => UpdateStatus = e.EventArgs)
				.DisposeWith(Disposables);

			Observable.FromEventPattern<bool>(Synchronizer, nameof(Synchronizer.ResponseArrivedIsGenSocksServFail))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e => OnResponseArrivedIsGenSocksServFail(e.EventArgs))
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.FiltersLeft, x => x.DownloadingBlock, x => x.UseBitcoinCore, x => x.BitcoinCoreStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(tup =>
				{
					(int filtersLeft, bool downloadingBlock, bool useCore, RpcStatus coreStatus) = tup;
					if (filtersLeft == 0 && !downloadingBlock && (!useCore || coreStatus?.Synchronized is true))
					{
						TryRemoveStatus(StatusType.Synchronizing);
					}
					else
					{
						TryAddStatus(StatusType.Synchronizing);
					}
				});

			this.WhenAnyValue(x => x.Tor, x => x.Backend, x => x.Peers, x => x.UseBitcoinCore, x => x.BitcoinCoreStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(tup =>
				{
					(TorStatus tor, BackendStatus backend, int peers, bool useCore, RpcStatus coreStatus) = tup;

					// The source of the p2p connection comes from if we use Core for it or the network.
					var p2pConnected = useCore ? coreStatus?.Success is true : peers >= 1;
					if (tor == TorStatus.NotRunning || backend != BackendStatus.Connected || !p2pConnected)
					{
						TryAddStatus(StatusType.Connecting);
					}
					else
					{
						TryRemoveStatus(StatusType.Connecting);
					}
				});

			this.WhenAnyValue(x => x.UpdateStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async x =>
				{
					if (x.BackendCompatible)
					{
						TryRemoveStatus(StatusType.CriticalUpdate);
					}
					else
					{
						TryAddStatus(StatusType.CriticalUpdate);
					}

					if (x.ClientUpToDate)
					{
						TryRemoveStatus(StatusType.OptionalUpdate);
					}
					else
					{
						TryAddStatus(StatusType.OptionalUpdate);
					}

					UpdateAvailable = !x.ClientUpToDate;
					CriticalUpdateAvailable = !x.BackendCompatible;
				});

			UpdateCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					await IoHelpers.OpenBrowserAsync("https://wasabiwallet.io/#download");
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
					IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
				}
			}, this.WhenAnyValue(x => x.UpdateAvailable, x => x.CriticalUpdateAvailable, (x, y) => x || y));

			this.RaisePropertyChanged(nameof(UpdateCommand)); // The binding happens after the constructor. So, if the command is not in constructor, then we need this line.

			UpdateCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void OnResponseArrivedIsGenSocksServFail(bool isGenSocksServFail)
		{
			if (MainWindowViewModel.Instance is null)
			{
				return;
			}

			if (isGenSocksServFail)
			{
				// Is close band present?
				if (MainWindowViewModel.Instance.ModalDialog is { })
				{
					// Do nothing.
				}
				else
				{
					// Show GenSocksServFail dialog on OS-es we suspect Tor is outdated.
					var osDesc = RuntimeInformation.OSDescription;
					if (osDesc.Contains("16.04.1-Ubuntu", StringComparison.InvariantCultureIgnoreCase)
						|| osDesc.Contains("16.04.0-Ubuntu", StringComparison.InvariantCultureIgnoreCase))
					{
						MainWindowViewModel.Instance.ShowDialogAsync(new GenSocksServFailDialogViewModel()).GetAwaiter().GetResult();
					}
				}
			}
			else
			{
				// Is close band present?
				if (MainWindowViewModel.Instance.ModalDialog is { })
				{
					// Is it GenSocksServFail dialog?
					if (MainWindowViewModel.Instance.ModalDialog is GenSocksServFailDialogViewModel)
					{
						MainWindowViewModel.Instance.ModalDialog.Close(true);
					}
					else
					{
						// Do nothing.
					}
				}
				else
				{
					// Do nothing.
				}
			}
		}

		public void TryAddStatus(StatusType status, ushort percentage = 0)
		{
			try
			{
				ActiveStatuses.Set(new Status(status, percentage));
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public void TryRemoveStatus(params StatusType[] statuses)
		{
			try
			{
				ActiveStatuses.Complete(statuses);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
