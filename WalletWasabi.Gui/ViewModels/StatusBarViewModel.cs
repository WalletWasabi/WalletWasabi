﻿using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin.Protocol;
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
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Helpers;
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
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private NodesCollection Nodes { get; }
		private WasabiSynchronizer Synchronizer { get; }

		private bool UseTor { get; }

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

		public StatusBarViewModel(NodesCollection nodes, WasabiSynchronizer synchronizer, UpdateChecker updateChecker)
		{
			UpdateStatus = UpdateStatus.Latest;
			Nodes = nodes;
			Synchronizer = synchronizer;
			BlocksLeft = 0;
			FiltersLeft = synchronizer.GetFiltersLeft();
			UseTor = Global.Config.UseTor.Value; // Don't make it dynamic, because if you change this config settings only next time will it activate.

			Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Added))
				.Subscribe(x =>
				{
					SetPeers(Nodes.Count);
				}).DisposeWith(Disposables);

			Observable.FromEventPattern<NodeEventArgs>(nodes, nameof(nodes.Removed))
				.Subscribe(x =>
				{
					SetPeers(Nodes.Count);
				}).DisposeWith(Disposables);

			SetPeers(Nodes.Count);

			Observable.FromEventPattern<int>(typeof(WalletService), nameof(WalletService.ConcurrentBlockDownloadNumberChanged))
				.Subscribe(x =>
				{
					BlocksLeft = x.EventArgs;
				}).DisposeWith(Disposables);

			Observable.FromEventPattern(synchronizer, nameof(synchronizer.NewFilter)).Subscribe(x =>
			{
				FiltersLeft = Synchronizer.GetFiltersLeft();
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.TorStatus).Subscribe(status =>
			{
				SetTor(status);
				SetPeers(Nodes.Count);
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.BackendStatus).Subscribe(_ =>
			{
				Backend = Synchronizer.BackendStatus;
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.BestBlockchainHeight).Subscribe(_ =>
			{
				FiltersLeft = Synchronizer.GetFiltersLeft();
			}).DisposeWith(Disposables);

			synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Subscribe(usd =>
			{
				BtcPrice = $"${(long)usd}";
			}).DisposeWith(Disposables);

			Observable.FromEventPattern<bool>(synchronizer, nameof(synchronizer.ResponseArrivedIsGenSocksServFail))
				.Subscribe(e =>
				{
					OnResponseArrivedIsGenSocksServFail(e.EventArgs);
				}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.BlocksLeft).Subscribe(blocks =>
			{
				RefreshStatus();
			});

			this.WhenAnyValue(x => x.FiltersLeft).Subscribe(filters =>
			{
				RefreshStatus();
			});

			this.WhenAnyValue(x => x.Tor).Subscribe(tor =>
			{
				RefreshStatus();
			});

			this.WhenAnyValue(x => x.Backend).Subscribe(backend =>
			{
				RefreshStatus();
			});

			this.WhenAnyValue(x => x.Peers).Subscribe(peers =>
			{
				RefreshStatus();
			});

			this.WhenAnyValue(x => x.UpdateStatus).Subscribe(_ =>
			{
				RefreshStatus();
			});

			this.WhenAnyValue(x => x.Status).Subscribe(async status =>
			{
				if (status.EndsWith(".")) // Then do animation.
				{
					string nextAnimation = null;
					if (status.EndsWith("..."))
					{
						nextAnimation = status.TrimEnd("..", StringComparison.Ordinal);
					}
					else if (status.EndsWith("..") || status.EndsWith("."))
					{
						nextAnimation = $"{status}.";
					}

					if (nextAnimation != null)
					{
						await Task.Delay(1000);
						if (Status == status) // If still the same.
						{
							Status = nextAnimation;
						}
					}
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
					Logging.Logger.LogWarning<StatusBarViewModel>(ex);
					IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
				}
			}, this.WhenAnyValue(x => x.UpdateStatus).Select(x => x != UpdateStatus.Latest));

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

		public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

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

		private List<string> StatusQueue { get; } = new List<string>();
		private object StatusQueueLock { get; } = new object();

		public void AddStatus(string status)
		{
			status = Guard.Correct(status);
			if (status == "")
			{
				return;
			}

			lock (StatusQueueLock)
			{
				// Make sure it's the last status.
				StatusQueue.Remove(status);
				StatusQueue.Add(status);
				RefreshStatusNoLock();
			}
		}

		public void RemoveStatus(string status)
		{
			status = Guard.Correct(status);
			if (status == "")
			{
				return;
			}

			lock (StatusQueueLock)
			{
				if (StatusQueue.Remove(status))
				{
					RefreshStatusNoLock();
				}
			}
		}

		private void RefreshStatus()
		{
			lock (StatusQueueLock)
			{
				if (!SetPriorityStatus())
				{
					SetCustomStatusOrReady();
				}
			}
		}

		private void RefreshStatusNoLock()
		{
			if (!SetPriorityStatus())
			{
				SetCustomStatusOrReady();
			}
		}

		private void SetCustomStatusOrReady()
		{
			var status = StatusQueue.LastOrDefault();
			if (status is null)
			{
				Status = "Ready";
			}
			else
			{
				Status = status;
			}
		}

		private bool SetPriorityStatus()
		{
			if (UpdateStatus == UpdateStatus.Critical)
			{
				Status = "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR SOFTWARE";
			}
			else if (UpdateStatus == UpdateStatus.Optional)
			{
				Status = "New Version Is Available";
			}
			else if (Tor == TorStatus.NotRunning || Backend != BackendStatus.Connected || Peers < 1)
			{
				Status = "Connecting...";
			}
			else if (FiltersLeft != 0 || BlocksLeft != 0)
			{
				Status = "Synchronizing...";
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

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
