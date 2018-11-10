using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.Protocol;
using ReactiveUI;
using WalletWasabi.Services;
using Avalonia.Data.Converters;
using System.Globalization;
using WalletWasabi.Models;
using NBitcoin;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using WalletWasabi.Gui.Tabs;
using System.Reactive.Linq;
using WalletWasabi.Gui.Dialogs;
using System.Runtime.InteropServices;

namespace WalletWasabi.Gui.ViewModels
{
	public enum UpdateStatus
	{
		Latest,
		Optional,
		Critical
	}

	public class StatusBarViewModel : ViewModelBase, IDisposable
	{
		public NodesCollection Nodes { get; }
		public MemPoolService MemPoolService { get; }
		public IndexDownloader IndexDownloader { get; }
		public UpdateChecker UpdateChecker { get; }

		private UpdateStatus _updateStatus;

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

		public ReactiveCommand UpdateCommand { get; }

		private bool _updateAvailable;

		public bool UpdateAvailable
		{
			get => _updateAvailable;
			set => this.RaiseAndSetIfChanged(ref _updateAvailable, value);
		}

		private bool _criticalUpdateAvailable;

		public bool CriticalUpdateAvailable
		{
			get => _criticalUpdateAvailable;
			set => this.RaiseAndSetIfChanged(ref _criticalUpdateAvailable, value);
		}

		private BackendStatus _backend;

		public BackendStatus Backend
		{
			get { return _backend; }
			set
			{
				this.RaiseAndSetIfChanged(ref _backend, value);
			}
		}

		private TorStatus _tor;

		public TorStatus Tor
		{
			get { return _tor; }
			set
			{
				this.RaiseAndSetIfChanged(ref _tor, value);
			}
		}

		private int _peers;

		public int Peers
		{
			get { return _peers; }
			set
			{
				this.RaiseAndSetIfChanged(ref _peers, value);
			}
		}

		private int _filtersLeft;

		public int FiltersLeft
		{
			get { return _filtersLeft; }
			set
			{
				this.RaiseAndSetIfChanged(ref _filtersLeft, value);
			}
		}

		private int _blocksLeft;

		public int BlocksLeft
		{
			get { return _blocksLeft; }
			set
			{
				this.RaiseAndSetIfChanged(ref _blocksLeft, value);
			}
		}

		private int _mempool;

		public int Mempool
		{
			get { return _mempool; }
			set
			{
				this.RaiseAndSetIfChanged(ref _mempool, value);
			}
		}

		private string _status;

		public string Status
		{
			get { return _status; }
			set { this.RaiseAndSetIfChanged(ref _status, value); }
		}

		private long _clientOutOfDate;
		private long _backendIncompatible;

		public StatusBarViewModel(NodesCollection nodes, MemPoolService memPoolService, IndexDownloader indexDownloader, UpdateChecker updateChecker)
		{
			_clientOutOfDate = 0;
			_backendIncompatible = 0;
			UpdateStatus = UpdateStatus.Latest;

			Nodes = nodes;
			Nodes.Added += Nodes_Added;
			Nodes.Removed += Nodes_Removed;
			Peers = Nodes.Count;

			BlocksLeft = 0;
			WalletService.ConcurrentBlockDownloadNumberChanged += WalletService_ConcurrentBlockDownloadNumberChanged;

			MemPoolService = memPoolService;
			MemPoolService.TransactionReceived += MemPoolService_TransactionReceived;
			Mempool = MemPoolService.TransactionHashes.Count;

			IndexDownloader = indexDownloader;
			UpdateChecker = updateChecker;
			IndexDownloader.NewFilter += IndexDownloader_NewFilter;
			IndexDownloader.TorStatusChanged += IndexDownloader_TorStatusChanged;
			IndexDownloader.BackendStatusChanged += IndexDownloader_BackendStatusChanged;
			IndexDownloader.ResponseArrivedIsGenSocksServFail += IndexDownloader_ResponseArrivedIsGenSocksServFail;

			FiltersLeft = IndexDownloader.GetFiltersLeft();

			IndexDownloader.WhenAnyValue(x => x.BestHeight).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				FiltersLeft = IndexDownloader.GetFiltersLeft();
			});

			this.WhenAnyValue(x => x.BlocksLeft).Subscribe(blocks =>
			{
				SetStatusAndDoUpdateActions();
			});
			this.WhenAnyValue(x => x.FiltersLeft).Subscribe(filters =>
			{
				SetStatusAndDoUpdateActions();
			});
			this.WhenAnyValue(x => x.Tor).Subscribe(tor =>
			{
				SetStatusAndDoUpdateActions();
			});
			this.WhenAnyValue(x => x.Backend).Subscribe(backend =>
			{
				SetStatusAndDoUpdateActions();
			});
			this.WhenAnyValue(x => x.Peers).Subscribe(peers =>
			{
				SetStatusAndDoUpdateActions();
			});

			UpdateChecker.Start(TimeSpan.FromMinutes(7),
				() =>
				{
					Interlocked.Exchange(ref _backendIncompatible, 1);
					Dispatcher.UIThread.Post(() =>
					{
						SetStatusAndDoUpdateActions();
					});
					return Task.CompletedTask;
				},
				() =>
				{
					Interlocked.Exchange(ref _clientOutOfDate, 1);
					Dispatcher.UIThread.Post(() =>
					{
						SetStatusAndDoUpdateActions();
					});
					return Task.CompletedTask;
				});

			UpdateCommand = ReactiveCommand.Create(() =>
			{
				IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
			}, this.WhenAnyValue(x => x.UpdateStatus).Select(x => x != UpdateStatus.Latest));
		}

		private void IndexDownloader_ResponseArrivedIsGenSocksServFail(object sender, bool isGenSocksServFail)
		{
			if (isGenSocksServFail)
			{
				// Is close band present?
				if (!(MainWindowViewModel.Instance.ModalDialog is null))
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
						MainWindowViewModel.Instance.ShowDialogAsync(new GenSocksServFailDialogViewModel()).GetAwaiter();
					}
				}
			}
			else
			{
				// Is close band present?
				if (!(MainWindowViewModel.Instance.ModalDialog is null))
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

		private void WalletService_ConcurrentBlockDownloadNumberChanged(object sender, int concurrentBlockDownloadNumber)
		{
			BlocksLeft = concurrentBlockDownloadNumber;
		}

		private void SetStatusAndDoUpdateActions()
		{
			if (Interlocked.Read(ref _backendIncompatible) != 0)
			{
				Status = "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR SOFTWARE";
				UpdateStatus = UpdateStatus.Critical;
			}
			else if (Interlocked.Read(ref _clientOutOfDate) != 0)
			{
				Status = "New Version Is Available";
				UpdateStatus = UpdateStatus.Optional;
			}
			else if (Tor != TorStatus.Running || Backend != BackendStatus.Connected || Peers < 1)
			{
				Status = "Connecting...";
			}
			else if (FiltersLeft != 0 || BlocksLeft != 0)
			{
				Status = "Synchronizing...";
			}
			else
			{
				Status = "Ready";
			}
		}

		private void IndexDownloader_BackendStatusChanged(object sender, BackendStatus e)
		{
			Backend = e;
		}

		private void IndexDownloader_TorStatusChanged(object sender, TorStatus e)
		{
			Tor = e;
		}

		private void IndexDownloader_NewFilter(object sender, Backend.Models.FilterModel e)
		{
			FiltersLeft = IndexDownloader.GetFiltersLeft();
		}

		private void MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
			Mempool = MemPoolService.TransactionHashes.Count;
		}

		private void Nodes_Removed(object sender, NodeEventArgs e)
		{
			Peers = Nodes.Count;
		}

		private void Nodes_Added(object sender, NodeEventArgs e)
		{
			Peers = Nodes.Count;
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Nodes.Added -= Nodes_Added;
					Nodes.Removed -= Nodes_Removed;
					MemPoolService.TransactionReceived -= MemPoolService_TransactionReceived;
					IndexDownloader.NewFilter -= IndexDownloader_NewFilter;
					IndexDownloader.TorStatusChanged -= IndexDownloader_TorStatusChanged;
					IndexDownloader.BackendStatusChanged -= IndexDownloader_BackendStatusChanged;
					IndexDownloader.ResponseArrivedIsGenSocksServFail -= IndexDownloader_ResponseArrivedIsGenSocksServFail;
				}

				_disposedValue = true;
			}
		}

		// ~StatusBarViewModel() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
