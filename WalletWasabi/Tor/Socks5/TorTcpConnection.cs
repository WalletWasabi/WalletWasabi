using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Socks5;

/// <summary>
/// Wraps a TCP connection to Tor SOCKS5 endpoint.
/// </summary>
public class TorTcpConnection : IDisposable
{
	private volatile bool _disposedValue = false;

	/// <summary>ID generator.</summary>
	private static long LastId;

	/// <param name="tcpClient">TCP client connected to Tor SOCKS5 endpoint.</param>
	/// <param name="transportStream">Transport stream to actually send the data to Tor SOCKS5 endpoint (the difference is SSL).</param>
	/// <param name="circuit">Tor circuit under which we operate with this TCP connection.</param>
	/// <param name="allowRecycling">Whether it is allowed to re-use this Tor TCP connection.</param>
	public TorTcpConnection(TcpClient tcpClient, Stream transportStream, ICircuit circuit, bool allowRecycling)
	{
		long id = Interlocked.Increment(ref LastId);
		string prefix = circuit switch
		{
			DefaultCircuit _ => "DC",
			PersonCircuit _ => "PC",
			_ => "UC" // Unknown circuit type.
		};
		Name = $"{prefix}#{id:0000}#{circuit.Name[0..10]}";

		TcpClient = tcpClient;
		TransportStream = transportStream;
		Circuit = circuit;
		AllowRecycling = allowRecycling;
	}

	/// <remarks>Lock object to guard <see cref="State"/> property.</remarks>
	private object StateLock { get; } = new();

	/// <remarks>All access to this property must be guarded by <see cref="StateLock"/>.</remarks>
	private TcpConnectionState State { get; set; }

	/// <summary>Gets whether this pool item can be potentially re-used.</summary>
	private bool AllowRecycling { get; }

	/// <summary>Gets whether internal <see cref="TcpClient"/> can be re-used for a new HTTP(s) request.</summary>
	/// <returns><c>true</c> when <see cref="TorTcpConnection"/> must be disposed, <c>false</c> otherwise.</returns>
	public bool NeedDisposal
	{
		get
		{
			lock (StateLock)
			{
				return State == TcpConnectionState.ToDispose || !Circuit.IsActive;
			}
		}
	}

	/// <summary>Unique identifier of the TCP connection for logging purposes.</summary>
	private string Name { get; }

	/// <summary>TCP connection to Tor's SOCKS5 server.</summary>
	private TcpClient TcpClient { get; }

	/// <summary>Transport stream for sending  HTTP/HTTPS requests through Tor's SOCKS5 server.</summary>
	/// <remarks>This stream is not to be used to send commands to Tor's SOCKS5 server.</remarks>
	private Stream TransportStream { get; }

	/// <summary>Tor circuit under which this TCP connection operates.</summary>
	public ICircuit Circuit { get; }

	/// <summary>
	/// Stream to transport HTTP(s) request.
	/// </summary>
	/// <remarks>Either <see cref="TcpClient.GetStream"/> or <see cref="SslStream"/> over <see cref="TcpClient.GetStream"/>.</remarks>
	public Stream GetTransportStream() => TransportStream;

	/// <summary>Reserve the pool item for an HTTP(s) request so no other consumer can use this pool item.</summary>
	public bool TryReserve()
	{
		lock (StateLock)
		{
			if (State == TcpConnectionState.FreeToUse)
			{
				State = TcpConnectionState.InUse;
				return true;
			}

			return false;
		}
	}

	/// <summary>
	/// After the <see cref="TorTcpConnection"/> is used to send an HTTP(s) request, it needs to be unreserved
	/// so that the pool item can be used again.
	/// </summary>
	/// <returns>Pool item state after unreserve operation.</returns>
	public TcpConnectionState Unreserve()
	{
		lock (StateLock)
		{
			Debug.Assert(State == TcpConnectionState.InUse, $"Unexpected state: '{State}'.");
			State = AllowRecycling ? TcpConnectionState.FreeToUse : TcpConnectionState.ToDispose;
			return State;
		}
	}

	/// <param name="force">
	/// <c>true</c> if we know that we have just finished using the TCP connection.
	/// <c>false</c> if we got an external piece of information that the TCP connection should be closed.
	/// </param>
	/// <remarks>Connection transporting an HTTP request that was canceled or otherwise failed must be torn down.</remarks>
	public void MarkAsToDispose(bool force = true)
	{
		lock (StateLock)
		{
			if (force)
			{
				Debug.Assert(State == TcpConnectionState.InUse, $"Unexpected state: '{State}'.");
				State = TcpConnectionState.ToDispose;
			}
			else if (State == TcpConnectionState.FreeToUse)
			{
				State = TcpConnectionState.ToDispose;
			}
		}
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return Name;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				TcpClient?.Dispose();
			}
			_disposedValue = true;
		}
	}

	/// <summary>
	/// This code added to correctly implement the disposable pattern.
	/// </summary>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		Dispose(true);
	}
}
