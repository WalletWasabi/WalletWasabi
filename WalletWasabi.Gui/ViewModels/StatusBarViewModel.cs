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
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
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
		private ObservableAsPropertyHelper<int> _filtersLeft;
		private string _btcPrice;
		private ObservableAsPropertyHelper<StatusBarStatus> _status;
		private bool _downloadingBlock;
		public Global Global { get; }
		private StatusBarStatusSet ActiveStatuses { get; }

		public StatusBarViewModel(Global global)
		{
			Global = global;
			UpdateStatus = UpdateStatus.Latest;
			UpdateAvailable = false;
			CriticalUpdateAvailable = false;
			Backend = BackendStatus.NotConnected;
			UseTor = false;
			Tor = TorStatus.NotRunning;
			Peers = 0;
			BtcPrice = "$0";
			ActiveStatuses = new StatusBarStatusSet();
		}

		public void Initialize(NodesCollection nodes, WasabiSynchronizer synchronizer, UpdateChecker updateChecker)
		{
			Nodes = nodes;
			Synchronizer = synchronizer;
			HashChain = synchronizer.BitcoinStore.HashChain;
			UseTor = Global.Config.UseTor; // Do not make it dynamic, because if you change this config settings only next time will it activate.

			_status = ActiveStatuses.WhenAnyValue(x => x.CurrentStatus)
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

			Observable.FromEventPattern<bool>(Synchronizer, nameof(Synchronizer.ResponseArrivedIsGenSocksServFail))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e => OnResponseArrivedIsGenSocksServFail(e.EventArgs))
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.FiltersLeft, x => x.DownloadingBlock)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(tup =>
				{
					(int filtersLeft, bool downloadingBlock) = tup.ToValueTuple();
					if (filtersLeft == 0 && !downloadingBlock)
					{
						TryRemoveStatus(StatusBarStatus.Synchronizing);
					}
					else
					{
						TryAddStatus(StatusBarStatus.Synchronizing);
					}
				});

			this.WhenAnyValue(x => x.Tor, x => x.Backend, x => x.Peers)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(tup =>
				{
					(TorStatus tor, BackendStatus backend, int peers) = tup.ToValueTuple();
					if (tor == TorStatus.NotRunning || backend != BackendStatus.Connected || peers < 1)
					{
						TryAddStatus(StatusBarStatus.Connecting);
					}
					else
					{
						TryRemoveStatus(StatusBarStatus.Connecting);
					}
				});

			this.WhenAnyValue(x => x.UpdateStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x == UpdateStatus.Critical)
					{
						TryAddStatus(StatusBarStatus.CriticalUpdate);
					}
					else
					{
						TryRemoveStatus(StatusBarStatus.CriticalUpdate);
					}

					if (x == UpdateStatus.Optional)
					{
						TryAddStatus(StatusBarStatus.OptionalUpdate);
					}
					else
					{
						TryRemoveStatus(StatusBarStatus.OptionalUpdate);
					}
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
			}, this.WhenAnyValue(x => x.UpdateStatus)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => x != UpdateStatus.Latest));
			this.RaisePropertyChanged(nameof(UpdateCommand)); // The binding happens after the constructor. So, if the command is not in constructor, then we need this line.

			Observable.FromEventPattern<UpdateStatusResult>(updateChecker, nameof(updateChecker.UpdateChecked))
				.Select(x => x.EventArgs)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.BackendCompatible)
					{
						if (x.ClientUpToDate)
						{
							UpdateStatus = UpdateStatus.Latest;
						}
						else
						{
							UpdateStatus = UpdateStatus.Optional;
						}
					}
					else
					{
						UpdateStatus = UpdateStatus.Critical;
					}

					UpdateAvailable = !x.ClientUpToDate;
					CriticalUpdateAvailable = !x.BackendCompatible;
				}).DisposeWith(Disposables);

			updateChecker.Start(TimeSpan.FromMinutes(7));
		}

		public ReactiveCommand<Unit, Unit> UpdateCommand { get; set; }

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

		public string BtcPrice
		{
			get => _btcPrice;
			set => this.RaiseAndSetIfChanged(ref _btcPrice, value);
		}

		public StatusBarStatus Status => _status?.Value ?? StatusBarStatus.Loading;

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

		public void TryAddStatus(StatusBarStatus status)
		{
			try
			{
				ActiveStatuses.TryAddStatus(status);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public void TryRemoveStatus(params StatusBarStatus[] statuses)
		{
			try
			{
				ActiveStatuses.TryRemoveStatus(statuses);
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
