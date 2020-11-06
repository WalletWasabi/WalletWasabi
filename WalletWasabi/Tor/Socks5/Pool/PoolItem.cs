using System;
using System.Diagnostics;
using System.Threading;

namespace WalletWasabi.Tor.Socks5.Pool
{
	/// <summary>
	/// TODO.
	/// </summary>
	public class PoolItem
	{
		private static long Counter;

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public PoolItem(TorSocks5Client client)
		{
			Id = Interlocked.Increment(ref Counter);
			State = PoolItemState.InUse;
			Client = client;
		}

		private object StateLock { get; } = new object();

		public PoolItemState State { get; private set; }
		private TorSocks5Client Client { get; set; }
		private long Id { get; }

		private bool _disposedValue;

		public TorSocks5Client GetClient()
		{
			lock (StateLock)
			{
				Debug.Assert(State == PoolItemState.InUse);
			}

			return Client;
		}

		/// <summary>
		/// TODO: Always recycles at this point.
		/// </summary>
		/// <returns></returns>
		public bool NeedRecycling()
		{
			lock (StateLock)
			{
				return (State == PoolItemState.FreeToUse) && !Client.IsConnected;
			}
		}

		public bool TryReserve()
		{
			lock (StateLock)
			{
				if (State == PoolItemState.FreeToUse && Client.IsConnected)
				{
					State = PoolItemState.InUse;
					return true;
				}
			}

			return false;
		}

		public void Unreserve()
		{
			lock (StateLock)
			{
				Debug.Assert(State == PoolItemState.InUse);
				State = PoolItemState.FreeToUse;
			}
		}

		/// <inheritdoc/>
		public override string? ToString()
		{
			return $"PoolItem#{Id}";
		}

		/// <summary>
		/// <list type="bullet">
		/// <item>Unmanaged resources need to be released regardless of the value of the <paramref name="disposing"/> parameter.</item>
		/// <item>Managed resources need to be released if the value of <paramref name="disposing"/> is <c>true</c>.</item>
		/// </list>
		/// </summary>
		/// <param name="disposing">
		/// Indicates whether the method call comes from a <see cref="Dispose()"/> method
		/// (its value is <c>true</c>) or from a finalizer (its value is <c>false</c>).
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Client?.Dispose();
					lock (StateLock)
					{
						State = PoolItemState.Disconnected;
					}
				}
				_disposedValue = true;
			}
		}

		/// <summary>
		/// Do not change this code.
		/// </summary>
		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);
			// Suppress finalization.
			GC.SuppressFinalize(this);
		}
	}

	public enum PoolItemState
	{
		InUse,
		FreeToUse,
		Disconnected
	}
}