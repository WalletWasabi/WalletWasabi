﻿using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin.Protocol;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Models;
using WalletWasabi.Services;

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
		private CompositeDisposable Disposables { get; }

		private UpdateStatus _updateStatus;
		private bool _updateAvailable;
		private bool _criticalUpdateAvailable;
		private BackendStatus _backend;
		private TorStatus _tor;
		private int _peers;
		private int _filtersLeft;
		private int _blocksLeft;
		private string _btcPrice;
		private string _status;
		private long _clientOutOfDate;
		private long _backendIncompatible;

		public StatusBarViewModel(NodesCollection nodes, WasabiSynchronizer synchronizer, UpdateChecker updateChecker)
		{
			Disposables = new CompositeDisposable();

			_clientOutOfDate = 0;
			_backendIncompatible = 0;

			UpdateStatus = UpdateStatus.Latest;
			Peers = nodes.Count;
			BlocksLeft = 0;
			FiltersLeft = synchronizer.GetFiltersLeft();

			Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Added))
				.Subscribe(x =>
				{
					Peers = nodes.Count;
				}).DisposeWith(Disposables);

			Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed))
				.Subscribe(x =>
				{
					Peers = nodes.Count;
				}).DisposeWith(Disposables);

			Observable.FromEventPattern<int>(typeof(WalletService), nameof(WalletService.ConcurrentBlockDownloadNumberChanged))
				.Subscribe(x =>
				{
					BlocksLeft = x.EventArgs;
				}).DisposeWith(Disposables);

			Observable.FromEventPattern(synchronizer, nameof(synchronizer.NewFilter)).Subscribe(x =>
			{
				FiltersLeft = synchronizer.GetFiltersLeft();
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.TorStatus).Subscribe(_ =>
			{
				Tor = synchronizer.TorStatus;
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.BackendStatus).Subscribe(_ =>
			{
				Backend = synchronizer.BackendStatus;
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.BestBlockchainHeight).Subscribe(_ =>
			{
				FiltersLeft = synchronizer.GetFiltersLeft();
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Subscribe(_ =>
			{
				BtcPrice = $"${(long)synchronizer.UsdExchangeRate}";
			}).DisposeWith(Disposables);

			Observable.FromEventPattern<bool>(synchronizer, nameof(synchronizer.ResponseArrivedIsGenSocksServFail))
				.Subscribe(e =>
				{
					OnResponseArrivedIsGenSocksServFail(e.EventArgs);
				}).DisposeWith(Disposables);

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
			}, this.WhenAnyValue(x => x.UpdateStatus).Select(x => x != UpdateStatus.Latest));

			updateChecker.Start(TimeSpan.FromMinutes(7),
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
		}

		public ReactiveCommand UpdateCommand { get; }

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

		public string Status
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
						MainWindowViewModel.Instance.ShowDialogAsync(new GenSocksServFailDialogViewModel()).GetAwaiter();
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

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
