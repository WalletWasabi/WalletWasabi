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
using System.Reactive.Disposables;
using System.ComponentModel;

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
		private CompositeDisposable Disposables { get; }

		public NodesCollection Nodes { get; }
		public WasabiSynchronizer Synchronizer { get; }
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
			get => _backend;
			set => this.RaiseAndSetIfChanged(ref _backend, value);
		}

		private TorStatus _tor;

		public TorStatus Tor
		{
			get => _tor;
			set => this.RaiseAndSetIfChanged(ref _tor, value);
		}

		private int _peers;

		public int Peers
		{
			get => _peers;
			set => this.RaiseAndSetIfChanged(ref _peers, value);
		}

		private int _filtersLeft;

		public int FiltersLeft
		{
			get => _filtersLeft;
			set => this.RaiseAndSetIfChanged(ref _filtersLeft, value);
		}

		private int _blocksLeft;

		public int BlocksLeft
		{
			get => _blocksLeft;
			set => this.RaiseAndSetIfChanged(ref _blocksLeft, value);
		}

		private string _btcPrice;

		public string BtcPrice
		{
			get => _btcPrice;
			set => this.RaiseAndSetIfChanged(ref _btcPrice, value);
		}

		private string _status;

		public string Status
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		private long _clientOutOfDate;
		private long _backendIncompatible;

		public StatusBarViewModel(NodesCollection nodes, WasabiSynchronizer synchronizer, UpdateChecker updateChecker)
		{
			Disposables = new CompositeDisposable();

			_clientOutOfDate = 0;
			_backendIncompatible = 0;
			UpdateStatus = UpdateStatus.Latest;

			Nodes = nodes;
			Nodes.Added += Nodes_Added;
			Nodes.Removed += Nodes_Removed;
			Peers = Nodes.Count;

			BlocksLeft = 0;
			WalletService.ConcurrentBlockDownloadNumberChanged += WalletService_ConcurrentBlockDownloadNumberChanged;

			Synchronizer = synchronizer;
			UpdateChecker = updateChecker;
			Synchronizer.NewFilter += IndexDownloader_NewFilter;
			Synchronizer.PropertyChanged += Synchronizer_PropertyChanged;
			Synchronizer.ResponseArrivedIsGenSocksServFail += IndexDownloader_ResponseArrivedIsGenSocksServFail;

			FiltersLeft = Synchronizer.GetFiltersLeft();

			this.WhenAnyValue(x => x.BlocksLeft).Subscribe(blocks =>
			{
				SetStatusAndDoUpdateActions();
			}).DisposeWith(Disposables);
			this.WhenAnyValue(x => x.FiltersLeft).Subscribe(filters =>
			{
				SetStatusAndDoUpdateActions();
			}).DisposeWith(Disposables);
			this.WhenAnyValue(x => x.Tor).Subscribe(tor =>
			{
				SetStatusAndDoUpdateActions();
			}).DisposeWith(Disposables);
			this.WhenAnyValue(x => x.Backend).Subscribe(backend =>
			{
				SetStatusAndDoUpdateActions();
			}).DisposeWith(Disposables);
			this.WhenAnyValue(x => x.Peers).Subscribe(peers =>
			{
				SetStatusAndDoUpdateActions();
			}).DisposeWith(Disposables);

			UpdateChecker.Start(TimeSpan.FromMinutes(7),
				() =>
				{
					Interlocked.Exchange(ref _backendIncompatible, 1);
					Dispatcher.UIThread.PostLogException(() =>
					{
						SetStatusAndDoUpdateActions();
					});
					return Task.CompletedTask;
				},
				() =>
				{
					Interlocked.Exchange(ref _clientOutOfDate, 1);
					Dispatcher.UIThread.PostLogException(() =>
					{
						SetStatusAndDoUpdateActions();
					});
					return Task.CompletedTask;
				});

			UpdateCommand = ReactiveCommand.Create(() =>
			{
				IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
			}, this.WhenAnyValue(x => x.UpdateStatus).Select(x => x != UpdateStatus.Latest)).DisposeWith(Disposables);
		}

		private void Synchronizer_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Synchronizer.TorStatus))
			{
				Tor = Synchronizer.TorStatus;
			}
			else if (e.PropertyName == nameof(Synchronizer.BackendStatus))
			{
				Backend = Synchronizer.BackendStatus;
			}
			else if (e.PropertyName == nameof(Synchronizer.BestBlockchainHeight))
			{
				FiltersLeft = Synchronizer.GetFiltersLeft();
			}
			else if (e.PropertyName == nameof(Synchronizer.UsdExchangeRate))
			{
				BtcPrice = $"${(long)Synchronizer.UsdExchangeRate}";
			}
		}

		private void IndexDownloader_ResponseArrivedIsGenSocksServFail(object sender, bool isGenSocksServFail)
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
						MainWindowViewModel.Instance.ShowDialogAsync(new GenSocksServFailDialogViewModel().DisposeWith(Disposables)).GetAwaiter();
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

		private void IndexDownloader_NewFilter(object sender, Backend.Models.FilterModel e)
		{
			FiltersLeft = Synchronizer.GetFiltersLeft();
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
					Synchronizer.NewFilter -= IndexDownloader_NewFilter;
					Synchronizer.PropertyChanged -= Synchronizer_PropertyChanged;
					Synchronizer.ResponseArrivedIsGenSocksServFail -= IndexDownloader_ResponseArrivedIsGenSocksServFail;

					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
