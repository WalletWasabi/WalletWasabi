using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.Protocol;
using ReactiveUI;

namespace WalletWasabi.Gui.ViewModels
{
	public class StatusBarViewModel : ViewModelBase, IDisposable
	{
		private NodesCollection Nodes { get; }

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

		private string _status;

		public string Status
		{
			get { return _status; }
			set { this.RaiseAndSetIfChanged(ref _status, value); }
		}

		public StatusBarViewModel(NodesCollection nodes)
		{
			Nodes = nodes;
			Nodes.Added += Nodes_Added;
			Nodes.Removed += Nodes_Removed;

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
