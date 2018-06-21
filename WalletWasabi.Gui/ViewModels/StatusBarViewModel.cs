using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.Protocol;
using ReactiveUI;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.ViewModels
{
	public class StatusBarViewModel : ViewModelBase, IDisposable
	{
		public NodesCollection Nodes { get; }
		public MemPoolService MemPoolService { get; }
		public IndexDownloader IndexDownloader { get; }

		private int _connections;

		public int Connections
		{
			get { return _connections; }
			set
			{
				this.RaiseAndSetIfChanged(ref _connections, value);
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

		public StatusBarViewModel(NodesCollection nodes, MemPoolService memPoolService, IndexDownloader indexDownloader)
		{
			Nodes = nodes;
			Nodes.Added += Nodes_Added;
			Nodes.Removed += Nodes_Removed;
			Connections = Nodes.Count;

			MemPoolService = memPoolService;
			MemPoolService.TransactionReceived += MemPoolService_TransactionReceived;
			Mempool = MemPoolService.TransactionHashes.Count;

			IndexDownloader = indexDownloader;
			IndexDownloader.NewFilter += IndexDownloader_NewFilter;
			FiltersLeft = IndexDownloader.GetFiltersLeft();

			this.WhenAnyValue(x => x.BlocksLeft).Subscribe(blocks =>
			{
				if (blocks > 0)
				{
					Status = "Synchronizing...";
				}
				else
				{
					Status = "Ready";
				}
			});
		}

		private void IndexDownloader_NewFilter(object sender, Backend.Models.FilterModel e)
		{
			FiltersLeft = IndexDownloader.GetFiltersLeft();
		}

		private void MemPoolService_TransactionReceived(object sender, Models.SmartTransaction e)
		{
			Mempool = MemPoolService.TransactionHashes.Count;
		}

		private void Nodes_Removed(object sender, NodeEventArgs e)
		{
			Connections = Nodes.Count;
		}

		private void Nodes_Added(object sender, NodeEventArgs e)
		{
			Connections = Nodes.Count;
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
