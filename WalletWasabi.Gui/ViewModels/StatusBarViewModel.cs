using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Gui.ViewModels
{
	public enum UpdateStatus
	{
		Latest,
		Optional,
		Critical
	}

	public class StatusBarViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private NodesCollection Nodes { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private HashChain HashChain { get; set; }

		private bool UseTor { get; set; }

		private UpdateStatus _updateStatus;
		private bool _updateAvailable;
		private bool _criticalUpdateAvailable;
		private BackendStatus _backend;
		private TorStatus _tor;
		private int _peers;
		private int _filtersLeft;
		private int _blocksLeft;
		private string _btcPrice;
		private StatusBarStatus _status;
		private List<StatusBarStatus> StatusQueue { get; } = new List<StatusBarStatus>();
		private object StatusQueueLock { get; } = new object();

		public StatusBarViewModel()
		{
			UpdateStatus = UpdateStatus.Latest;
			UpdateAvailable = false;
			CriticalUpdateAvailable = false;
			Backend = BackendStatus.NotConnected;
			UseTor = false;
			Tor = TorStatus.NotRunning;
			Peers = 0;
			FiltersLeft = 0;
			BlocksLeft = 0;
			BtcPrice = "$0";
			Status = StatusBarStatus.Loading;
		}

		public void Initialize(NodesCollection nodes, WasabiSynchronizer synchronizer, UpdateChecker updateChecker)
		{
			Nodes = nodes;
			Synchronizer = synchronizer;
			HashChain = synchronizer.BitcoinStore.HashChain;
			UseTor = Global.Config.UseTor.Value; // Don't make it dynamic, because if you change this config settings only next time will it activate.

			Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Added))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					SetPeers(Nodes.Count);
				}).DisposeWith(Disposables);

			Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					SetPeers(Nodes.Count);
				}).DisposeWith(Disposables);

			SetPeers(Nodes.Count);

			Observable.FromEventPattern<int>(typeof(WalletService), nameof(WalletService.ConcurrentBlockDownloadNumberChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					BlocksLeft = x.EventArgs;
				}).DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.TorStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(status =>
				{
					SetTor(status);
					SetPeers(Nodes.Count);
				}).DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.BackendStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					Backend = Synchronizer.BackendStatus;
				}).DisposeWith(Disposables);

			HashChain.WhenAnyValue(x => x.HashesLeft)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					FiltersLeft = x;
				}).DisposeWith(Disposables);

			Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(usd =>
				{
					BtcPrice = $"${(long)usd}";
				}).DisposeWith(Disposables);

			Observable.FromEventPattern<bool>(Synchronizer, nameof(Synchronizer.ResponseArrivedIsGenSocksServFail))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e =>
				{
					OnResponseArrivedIsGenSocksServFail(e.EventArgs);
				}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.BlocksLeft)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(blocks =>
				{
					RefreshStatus();
				});

			this.WhenAnyValue(x => x.FiltersLeft)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(filters =>
				{
					RefreshStatus();
				});

			this.WhenAnyValue(x => x.Tor)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(tor =>
				{
					RefreshStatus();
				});

			this.WhenAnyValue(x => x.Backend)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(backend =>
				{
					RefreshStatus();
				});

			this.WhenAnyValue(x => x.Peers)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(peers =>
				{
					RefreshStatus();
				});

			this.WhenAnyValue(x => x.UpdateStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					RefreshStatus();
				});

			UpdateCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					IoHelpers.OpenBrowser("https://wasabiwallet.io/#download");
				}
				catch (Exception ex)
				{
					Logging.Logger.LogWarning<StatusBarViewModel>(ex);
					IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
				}
			}, this.WhenAnyValue(x => x.UpdateStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => x != UpdateStatus.Latest));

			updateChecker.Start(TimeSpan.FromMinutes(7),
				() =>
				{
					UpdateStatus = UpdateStatus.Critical;
					return Task.CompletedTask;
				},
				() =>
				{
					if (UpdateStatus != UpdateStatus.Critical)
					{
						UpdateStatus = UpdateStatus.Optional;
					}
					return Task.CompletedTask;
				});
		}

		public ReactiveCommand<Unit, Unit> UpdateCommand { get; set; }

		public UpdateStatus UpdateStatus
		{
			get => _updateStatus;
			set
			{
				if (value != UpdateStatus.Latest)
				{
					UpdateAvailable = true;

					if (value == UpdateStatus.Critical)
					{
						CriticalUpdateAvailable = true;
					}
				}

				this.RaiseAndSetIfChanged(ref _updateStatus, value);
			}
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

		public int FiltersLeft
		{
			get => _filtersLeft;
			set => this.RaiseAndSetIfChanged(ref _filtersLeft, value);
		}

		public int BlocksLeft
		{
			get => _blocksLeft;
			set => this.RaiseAndSetIfChanged(ref _blocksLeft, value);
		}

		public string BtcPrice
		{
			get => _btcPrice;
			set => this.RaiseAndSetIfChanged(ref _btcPrice, value);
		}

		public StatusBarStatus Status
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
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

		public void TryAddStatus(StatusBarStatus status)
		{
			try
			{
				lock (StatusQueueLock)
				{
					// Make sure it's the last status.
					StatusQueue.Remove(status);
					StatusQueue.Add(status);
					RefreshStatusNoLock();
				}
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<StatusBarViewModel>(ex);
			}
		}

		public void TryRemoveStatus(params StatusBarStatus[] statuses)
		{
			try
			{
				lock (StatusQueueLock)
				{
					foreach (StatusBarStatus status in statuses)
					{
						if (StatusQueue.Remove(status))
						{
							RefreshStatusNoLock();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<StatusBarViewModel>(ex);
			}
		}

		private void RefreshStatus()
		{
			lock (StatusQueueLock)
			{
				RefreshStatusNoLock();
			}
		}

		private void RefreshStatusNoLock()
		{
			if (!TrySetPriorityStatus())
			{
				SetCustomStatusOrReady();
			}
		}

		private void SetCustomStatusOrReady()
		{
			Dispatcher.UIThread.PostLogException(() =>
			{
				if (StatusQueue.Any())
				{
					Status = StatusQueue.Last();
				}
				else
				{
					Status = StatusBarStatus.Ready;
				}
			});
		}

		private bool TrySetPriorityStatus()
		{
			if (UpdateStatus == UpdateStatus.Critical)
			{
				Status = StatusBarStatus.CriticalUpdate;
			}
			else if (UpdateStatus == UpdateStatus.Optional)
			{
				Status = StatusBarStatus.OptionalUpdate;
			}
			else if (Tor == TorStatus.NotRunning || Backend != BackendStatus.Connected || Peers < 1)
			{
				Status = StatusBarStatus.Connecting;
			}
			else if (FiltersLeft != 0 || BlocksLeft != 0)
			{
				Status = StatusBarStatus.Synchronizing;
			}
			else
			{
				return false;
			}

			return true;
		}

		private void SetPeers(int peers)
		{
			// Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it's seem to the user that peers are connected over clearnet, while they don't.
			Peers = Tor == TorStatus.NotRunning ? 0 : peers;
		}

		private void SetTor(TorStatus tor)
		{
			// Set peers to 0 if Tor is not running, because we get Tor status from backend answer so it's seem to the user that peers are connected over clearnet, while they don't.
			Tor = UseTor ? tor : TorStatus.TurnedOff;
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
