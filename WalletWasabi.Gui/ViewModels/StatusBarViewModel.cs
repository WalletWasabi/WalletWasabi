using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.ViewModels
{
	public class StatusBarViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private NodesCollection Nodes { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private SmartHeaderChain HashChain { get; set; }

		private bool UseTor { get; set; }

		private RpcStatus _bitcoinCoreStatus;
		private UpdateStatus _updateStatus;
		private bool _updateAvailable;
		private bool _criticalUpdateAvailable;

		private bool _useBitcoinCore;
		private BackendStatus _backend;
		private TorStatus _tor;
		private int _peers;
		private ObservableAsPropertyHelper<int> _filtersLeft;
		private string _btcPrice;
		private ObservableAsPropertyHelper<string> _status;
		private bool _downloadingBlock;
		public Global Global { get; }
		private StatusSet ActiveStatuses { get; }

		public StatusBarViewModel(Global global)
		{
			Global = global;
			Backend = BackendStatus.NotConnected;
			UseTor = false;
			Tor = TorStatus.NotRunning;
			Peers = 0;
			BtcPrice = "$0";
			ActiveStatuses = new StatusSet();
		}

		public void Initialize(NodesCollection nodes, WasabiSynchronizer synchronizer)
		{
			Nodes = nodes;
			Synchronizer = synchronizer;
			HashChain = synchronizer.BitcoinStore.SmartHeaderChain;
			UseTor = Global.Config.UseTor; // Do not make it dynamic, because if you change this config settings only next time will it activate.
			UseBitcoinCore = Global.Config.StartLocalBitcoinCoreOnStartup;
			var hostedServices = Global.HostedServices;

			var updateChecker = hostedServices.FirstOrDefault<UpdateChecker>();
			Guard.NotNull(nameof(updateChecker), updateChecker);
			UpdateStatus = updateChecker.UpdateStatus;

			var rpcMonitor = hostedServices.FirstOrDefault<RpcMonitor>();
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

			Observable.FromEventPattern<bool>(typeof(WalletService), nameof(WalletService.DownloadingBlockChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => DownloadingBlock = x.EventArgs)
				.DisposeWith(Disposables);

			IDisposable walletCheckingInterwal = null;
			Observable.FromEventPattern<bool>(typeof(WalletService), nameof(WalletService.InitializingChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.EventArgs)
					{
						TryAddStatus(StatusType.WalletLoading);

						if (walletCheckingInterwal is null)
						{
							walletCheckingInterwal = Observable.Interval(TimeSpan.FromSeconds(1))
							   .ObserveOn(RxApp.MainThreadScheduler)
							   .Subscribe(_ =>
							   {
								   var global = Global;
								   var walletService = global?.WalletService;
								   if (walletService is { })
								   {
									   if (walletService.LastProcessedFilter?.Header?.Height is uint lastProcessedFilterHeight
											&& global?.BitcoinStore?.SmartHeaderChain?.TipHeight is uint tipHeight)
									   {
										   var perc = tipHeight == 0 ?
												100
												: ((decimal)lastProcessedFilterHeight / tipHeight * 100);
										   TryAddStatus(StatusType.WalletProcessingFilters, (ushort)perc);
									   }

									   var txProcessor = walletService.TransactionProcessor;
									   if (txProcessor is { })
									   {
										   var perc = txProcessor.QueuedTxCount == 0 ?
												100
												: ((decimal)txProcessor.QueuedProcessedTxCount / txProcessor.QueuedTxCount * 100);
										   TryAddStatus(StatusType.WalletProcessingTransactions, (ushort)perc);
									   }
								   }
							   }).DisposeWith(Disposables);
						}
					}
					else
					{
						walletCheckingInterwal?.Dispose();
						walletCheckingInterwal = null;

						TryRemoveStatus(StatusType.WalletLoading, StatusType.WalletProcessingFilters, StatusType.WalletProcessingTransactions);
					}
				})
				.DisposeWith(Disposables);

			Observable.FromEventPattern<bool>(typeof(WalletService), nameof(WalletService.InitializingTransactionsChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.EventArgs)
					{
						TryAddStatus(StatusType.WalletProcessingTransactions);
					}
					else
					{
						TryRemoveStatus(StatusType.WalletProcessingTransactions);
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

			_filtersLeft = HashChain.WhenAnyValue(x => x.HashesLeft)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.ToProperty(this, x => x.FiltersLeft)
				.DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(usd => BtcPrice = $"${(long)usd}")
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

			this.WhenAnyValue(x => x.FiltersLeft, x => x.UseBitcoinCore, x => x.BitcoinCoreStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(tup =>
				{
					(int filtersLeft, bool useCore, RpcStatus coreStatus) = tup;
					if (filtersLeft == 0 && (!useCore || coreStatus?.Synchronized is true))
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
				.Subscribe(x =>
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

			UpdateCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					IoHelpers.OpenBrowser("https://wasabiwallet.io/#download");
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
					IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(Global));
				}
			}, this.WhenAnyValue(x => x.UpdateAvailable, x => x.CriticalUpdateAvailable, (x, y) => x || y));

			this.RaisePropertyChanged(nameof(UpdateCommand)); // The binding happens after the constructor. So, if the command is not in constructor, then we need this line.

			UpdateCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ReactiveCommand<Unit, Unit> UpdateCommand { get; set; }

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

		public string BtcPrice
		{
			get => _btcPrice;
			set => this.RaiseAndSetIfChanged(ref _btcPrice, value);
		}

		public string Status => _status?.Value ?? "Loading...";

		public bool DownloadingBlock
		{
			get => _downloadingBlock;
			set => this.RaiseAndSetIfChanged(ref _downloadingBlock, value);
		}

		private void OnResponseArrivedIsGenSocksServFail(bool isGenSocksServFail)
		{
			if (isGenSocksServFail)
			{
				// Is close band present?
				if (MainWindowViewModel.Instance.ModalDialog != null)
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
				if (MainWindowViewModel.Instance.ModalDialog != null)
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

		private volatile bool _disposedValue = false; // To detect redundant calls

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
