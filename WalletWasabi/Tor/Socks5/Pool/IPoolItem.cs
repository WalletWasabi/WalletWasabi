using System.IO;
using System.Net.Security;
using System.Net.Sockets;

namespace WalletWasabi.Tor.Socks5.Pool
{
	/// <summary>
	/// Base interface representing a single TCP connection with Tor SOCKS5.
	/// </summary>
	public interface IPoolItem
	{
		/// <summary>
		/// If returns <c>false</c>, then internal <see cref="GetTransportStream"/> can be re-used for a new HTTP(s) request.
		/// </summary>
		/// <returns><c>true</c> when <see cref="IPoolItem"/> resources must be released/disposed, <c>false</c> otherwise.</returns>
		bool NeedRecycling { get; }

		/// <summary>Reserve the pool item for an HTTP(s) request so no other consumer can use this pool item.</summary>
		bool TryReserve();

		/// <summary>Mark the pool item as "not-in-use anymore". The pool item can be re-used, if it is allowed.</summary>
		PoolItemState Unreserve();

		/// <summary>
		/// Stream used to communicate with Tor SOCKS5.
		/// </summary>
		/// <remarks>Typically either <see cref="NetworkStream"/> or <see cref="SslStream"/>.</remarks>
		Stream GetTransportStream();
	}
}
