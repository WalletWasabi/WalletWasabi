using System;
using ReactiveUI;

namespace WalletWasabi.Gui.ViewModels
{
	public class StatusBarViewModel : ViewModelBase, IDisposable
	{
		private bool _backend;

		public bool Backend
		{
			get { return _backend; }
			set
			{
				this.RaiseAndSetIfChanged(ref _backend, value);
			}
		}

		private bool _tor;

		public bool Tor
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

		public StatusBarViewModel()
		{
			Peers = 2;

			Mempool = 2;

			FiltersLeft = 2;

			this.WhenAnyValue(x => x.BlocksLeft).Subscribe(blocks =>
			{
				SetStatus();
			});
			this.WhenAnyValue(x => x.FiltersLeft).Subscribe(filters =>
			{
				SetStatus();
			});
			this.WhenAnyValue(x => x.Tor).Subscribe(tor =>
			{
				SetStatus();
			});
			this.WhenAnyValue(x => x.Backend).Subscribe(backend =>
			{
				SetStatus();
			});
			this.WhenAnyValue(x => x.Peers).Subscribe(peers =>
			{
				SetStatus();
			});
		}

		private void SetStatus()
		{
			if (FiltersLeft != 0 || BlocksLeft != 0)
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
