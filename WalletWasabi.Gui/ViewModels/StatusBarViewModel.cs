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

namespace WalletWasabi.Gui.ViewModels
{
	public class StatusBarViewModel : ViewModelBase, IDisposable
	{
		public NodesCollection Nodes { get; }
		public MemPoolService MemPoolService { get; }
		public IndexDownloader IndexDownloader { get; }
		public UpdateChecker UpdateChecker { get; }

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

		private string _showNetwork;

		public string ShowNetwork
		{
			get { return _showNetwork; }
			set { this.RaiseAndSetIfChanged(ref _showNetwork, value); }
		}

		private long _clientOutOfDate;
		private long _backendIncompatible;

		public StatusBarViewModel(NodesCollection nodes, MemPoolService memPoolService, IndexDownloader indexDownloader, UpdateChecker updateChecker)
		{
			_clientOutOfDate = 0;
			_backendIncompatible = 0;

			Nodes = nodes;
			Nodes.Added += Nodes_Added;
			Nodes.Removed += Nodes_Removed;
			Peers = Nodes.Count;

			MemPoolService = memPoolService;
			MemPoolService.TransactionReceived += MemPoolService_TransactionReceived;
			Mempool = MemPoolService.TransactionHashes.Count;

			IndexDownloader = indexDownloader;
			UpdateChecker = updateChecker;
			IndexDownloader.NewFilter += IndexDownloader_NewFilter;
			IndexDownloader.BestHeightChanged += IndexDownloader_BestHeightChanged;
			IndexDownloader.TorStatusChanged += IndexDownloader_TorStatusChanged;
			IndexDownloader.BackendStatusChanged += IndexDownloader_BackendStatusChanged;

			FiltersLeft = IndexDownloader.GetFiltersLeft();

			if (indexDownloader.Network == Network.Main)
			{
				ShowNetwork = "";
			}
			else
			{
				ShowNetwork = indexDownloader.Network.ToString();
			}

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
					SetStatusAndDoUpdateActions();
					return Task.CompletedTask;
				},
				() =>
				{
					Interlocked.Exchange(ref _clientOutOfDate, 1);
					SetStatusAndDoUpdateActions();
					return Task.CompletedTask;
				});
		}

		private void SetStatusAndDoUpdateActions()
		{
			if (Interlocked.Read(ref _backendIncompatible) != 0)
			{
				Status = "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR SOFTWARE";
				// ToDo: 1. Make the status bar clickable. Click takes the user to the About page. (Where the new release download link is.)
				// ToDo: 2. Remove all the text from the status bar except this one.
				// ToDo: 3. Make the background of the status bar "IndianRed". Other red is fine, but I think IndianRed would look the best.
			}
			else if (Interlocked.Read(ref _clientOutOfDate) != 0)
			{
				Status = "New Version Is Available";
				// ToDo: 1. Make the status bar clickable. Click takes the user to the About page. (Where the new release download link is.)
				// ToDo: 2. Add a green flag icon to the beginning of this text.
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

		private void IndexDownloader_BestHeightChanged(object sender, Height e)
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
					IndexDownloader.BestHeightChanged -= IndexDownloader_BestHeightChanged;
					IndexDownloader.TorStatusChanged -= IndexDownloader_TorStatusChanged;
					IndexDownloader.BackendStatusChanged -= IndexDownloader_BackendStatusChanged;
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
