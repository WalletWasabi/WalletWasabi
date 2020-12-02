using System;
using System.IO;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5
{
	public class TestPoolItem : IPoolItem
	{
		public TestPoolItem(PoolItemState state, bool allowRecycling, Stream? transportStream = null)
		{
			State = state;
			AllowRecycling = allowRecycling;
			TransportStream = transportStream;
		}

		public PoolItemState State { get; set; }
		public bool AllowRecycling { get; }


		/// <inheritdoc/>
		public bool NeedRecycling => State == PoolItemState.ToDispose;

		private Stream? TransportStream { get; }

		/// <inheritdoc/>
		public Stream GetTransportStream()
		{
			Guard.NotNull(nameof(TransportStream), TransportStream);
			return TransportStream!;
		}

		/// <inheritdoc/>
		public bool TryReserve()
		{
			if (State == PoolItemState.FreeToUse)
			{
				State = PoolItemState.InUse;
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public PoolItemState Unreserve()
		{
			State = NeedRecycling ? PoolItemState.FreeToUse : PoolItemState.ToDispose;
			return State;
		}
	}
}