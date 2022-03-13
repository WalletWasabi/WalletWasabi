using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Http.Models;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Socks5.Pool;

public enum TcpConnectionState
{
	/// <summary><see cref="TorTcpConnection"/> is in use currently.</summary>
	InUse,

	/// <summary><see cref="TorTcpConnection"/> can be used for a new HTTP request.</summary>
	FreeToUse,

	/// <summary><see cref="TorTcpConnection"/> is to be disposed.</summary>
	ToDispose
}

/// <summary>
/// The pool represents a set of multiple TCP connections to Tor SOCKS5 endpoint that are
/// stored in <see cref="TorTcpConnection"/>s.
/// </summary>
public class TorHttpPool : IDisposable
{
	/// <summary>Maximum number of <see cref="TorTcpConnection"/>s per URI host.</summary>
	/// <remarks>This parameter affects maximum parallelization for given URI host.</remarks>
	public const int MaxConnectionsPerHost = 100;

	private bool _disposedValue;

	public TorHttpPool(EndPoint endpoint)
		: this(new TorTcpConnectionFactory(endpoint))
	{
	}

	/// <summary>Constructor that helps in tests.</summary>
	internal TorHttpPool(TorTcpConnectionFactory tcpConnectionFactory)
	{
		TcpConnectionFactory = tcpConnectionFactory;
	}

	/// <summary>Key is always a URI host. Value is a list of pool connections that can connect to the URI host.</summary>
	/// <remarks>All access to this object must be guarded by <see cref="ObtainPoolConnectionLock"/>.</remarks>
	private Dictionary<string, List<TorTcpConnection>> ConnectionPerHost { get; } = new();

	/// <remarks>Lock object required for the combination of <see cref="TorTcpConnection"/> selection or creation in <see cref="ObtainFreeConnectionAsync(HttpRequestMessage, ICircuit, CancellationToken)"/>.</remarks>
	private AsyncLock ObtainPoolConnectionLock { get; } = new();

	private TorTcpConnectionFactory TcpConnectionFactory { get; }

	public static DateTimeOffset? TorDoesntWorkSince { get; private set; }

	public Task<bool> IsTorRunningAsync()
	{
		return TcpConnectionFactory.IsTorRunningAsync();
	}

	public static Exception? LatestTorException { get; private set; } = null;

	/// <summary>
	/// This method is called when an HTTP(s) request fails for some reason.
	/// <para>The information is stored to allow <see cref="TorMonitor"/> to restart Tor as deemed fit.</para>
	/// </summary>
	/// <param name="e">Tor exception.</param>
	private void OnTorRequestFailed(Exception e)
	{
		if (TorDoesntWorkSince is null)
		{
			TorDoesntWorkSince = DateTimeOffset.UtcNow;
		}

		LatestTorException = e;
	}

	/// <summary>
	/// Sends an HTTP(s) request.
	/// <para>HTTP(s) requests with loopback destination after forwarded to <see cref="ClearnetHttpClient"/> and that's it.</para>
	/// <para>When a new non-loopback HTTP(s) request comes, <see cref="TorTcpConnection"/> (or rather the TCP connection wrapped inside) is selected using these rules:
	/// <list type="number">
	/// <item>An unused <see cref="TorTcpConnection"/> is selected, if it exists.</item>
	/// <item>A new <see cref="TorTcpConnection"/> is added to the pool, if it would not exceed the maximum limit on the number of connections to Tor SOCKS5 endpoint.</item>
	/// <item>Keep waiting 1 second until any of the previous rules cannot be used.</item>
	/// </list>
	/// </para>
	/// <para><see cref="ObtainPoolConnectionLock"/> is acquired only for <see cref="TorTcpConnection"/> selection.</para>
	/// </summary>
	/// <exception cref="HttpRequestException">When <paramref name="request"/> fails to be processed.</exception>
	/// <exception cref="OperationCanceledException">When the operation was canceled.</exception>
	public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, ICircuit circuit, CancellationToken cancellationToken = default)
	{
		int i = 0;
		int attemptsNo = 3;
		TorTcpConnection? connection = null;

		try
		{
			do
			{
				i++;
				connection = await ObtainFreeConnectionAsync(request, circuit, cancellationToken).ConfigureAwait(false);
				TorTcpConnection? connectionToDispose = connection;

				try
				{
					Logger.LogTrace($"['{connection}'][Attempt #{i}] About to send request.");
					HttpResponseMessage response = await SendCoreAsync(connection, request, cancellationToken).ConfigureAwait(false);

					// Client works OK, no need to dispose.
					connectionToDispose = null;

					// Let others use the client.
					TcpConnectionState state = connection.Unreserve();
					Logger.LogTrace($"['{connection}'][Attempt #{i}] Unreserve. State is: '{state}'.");

					TorDoesntWorkSince = null;
					LatestTorException = null;

					return response;
				}
				catch (TorConnectionWriteException e)
				{
					Logger.LogTrace($"['{connection}'] TCP connection from the pool is probably dead as we can't write data to the connection.", e);

					if (i == attemptsNo)
					{
						Logger.LogDebug($"['{connection}'] All {attemptsNo} attempts failed.");
						throw new HttpRequestException("Failed to handle the HTTP request via Tor (write failure).", e);
					}
				}
				catch (TorConnectionReadException e)
				{
					Logger.LogTrace($"['{connection}'] Could not get/read an HTTP response from Tor.", e);

					throw new HttpRequestException("Failed to get/read an HTTP response from Tor.", e);
				}
				catch (TorCircuitExpiredException e)
				{
					Logger.LogTrace($"['{connection}'] Circuit '{circuit.Name}' has expired and cannot be used again.", e);

					throw new HttpRequestException($"Circuit '{circuit.Name}' has expired and cannot be used again.", e);
				}
				catch (TorConnectCommandFailedException e) when (e.RepField == RepField.TtlExpired)
				{
					// If we get TTL Expired error then wait and retry again, Linux often does this.
					Logger.LogTrace($"['{connection}'] TTL exception occurred.", e);

					await Task.Delay(3000, cancellationToken).ConfigureAwait(false);

					if (i == attemptsNo)
					{
						Logger.LogDebug($"['{connection}'] All {attemptsNo} attempts failed.");
						throw new HttpRequestException("Failed to handle the HTTP request via Tor.", e);
					}
				}
				catch (IOException e)
				{
					Logger.LogTrace($"['{connection}'] Failed to read/write HTTP(s) request.", e);

					// NetworkStream may throw IOException.
					TorConnectionException innerException = new("Failed to read/write HTTP(s) request.", e);
					throw new HttpRequestException("Failed to handle the HTTP request via Tor.", innerException);
				}
				catch (SocketException e) when (e.ErrorCode == (int)SocketError.ConnectionRefused)
				{
					Logger.LogTrace($"['{connection}'] Connection was refused.", e);
					TorConnectionException innerException = new("Connection was refused.", e);
					throw new HttpRequestException("Failed to handle the HTTP request via Tor.", innerException);
				}
				catch (Exception e)
				{
					Logger.LogTrace($"['{connection}'] Exception occurred.", e);
					throw;
				}
				finally
				{
					if (connectionToDispose is not null)
					{
						Logger.LogTrace($"['{connectionToDispose}'] marked as to be disposed.");
						connectionToDispose.MarkAsToDispose();
					}
				}
			}
			while (i < attemptsNo);
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace($"[{connection}] Request was canceled: '{request.RequestUri}'.");
			throw;
		}
		catch (Exception e)
		{
			Logger.LogTrace($"[{connection}] Request failed with exception", e);
			OnTorRequestFailed(e);
			throw;
		}

		throw new NotImplementedException("This should never happen.");
	}

	private async Task<TorTcpConnection> ObtainFreeConnectionAsync(HttpRequestMessage request, ICircuit circuit, CancellationToken token)
	{
		Logger.LogTrace($"> request='{request.RequestUri}', circuit={circuit}");

		string host = GetRequestHost(request);

		do
		{
			using (await ObtainPoolConnectionLock.LockAsync(token).ConfigureAwait(false))
			{
				bool canBeAdded = GetPoolConnectionNoLock(host, circuit, out TorTcpConnection? connection);

				if (connection is not null)
				{
					Logger.LogTrace($"[OLD {connection}]['{request.RequestUri}'] Re-use existing Tor SOCKS5 connection.");
					return connection;
				}

				// The circuit may be disposed almost immediately after this check but we don't mind.
				if (!circuit.IsActive)
				{
					throw new TorCircuitExpiredException();
				}

				if (canBeAdded)
				{
					connection = await CreateNewConnectionAsync(request, circuit, token).ConfigureAwait(false);

					if (connection is not null)
					{
						ConnectionPerHost[host].Add(connection);

						Logger.LogTrace($"[NEW {connection}]['{request.RequestUri}'] Using new Tor SOCKS5 connection.");
						return connection;
					}
				}
			}

			Logger.LogTrace("Wait 1s for a free pool connection.");
			await Task.Delay(1000, token).ConfigureAwait(false);
		}
		while (true);
	}

	private async Task<TorTcpConnection?> CreateNewConnectionAsync(HttpRequestMessage request, ICircuit circuit, CancellationToken cancellationToken)
	{
		TorTcpConnection? connection;

		try
		{
			connection = await TcpConnectionFactory.ConnectAsync(request.RequestUri!, circuit, cancellationToken).ConfigureAwait(false);
			Logger.LogTrace($"[NEW {connection}]['{request.RequestUri}'] Created new Tor SOCKS5 connection.");
		}
		catch (TorException e)
		{
			Logger.LogDebug($"['{request.RequestUri}'][ERROR] Failed to create a new pool connection.", e);
			throw;
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace($"['{request.RequestUri}'] Operation was canceled.");
			throw;
		}
		catch (Exception e)
		{
			Logger.LogTrace($"['{request.RequestUri}'][EXCEPTION] {e}");
			throw;
		}

		Logger.LogTrace($"< connection='{connection}'");
		return connection;
	}

	/// <exception cref="TorConnectionWriteException">When a failure during sending our HTTP(s) request to Tor SOCKS5 occurs.</exception>
	/// <exception cref="TorConnectionReadException">When a failure during receiving HTTP response from Tor SOCKS5 occurs.</exception>
	internal virtual async Task<HttpResponseMessage> SendCoreAsync(TorTcpConnection connection, HttpRequestMessage request, CancellationToken token = default)
	{
		// https://tools.ietf.org/html/rfc7230#section-2.6
		// Intermediaries that process HTTP messages (i.e., all intermediaries
		// other than those acting as tunnels) MUST send their own HTTP - version
		// in forwarded messages.
		request.Version = HttpProtocol.HTTP11.Version;
		request.Headers.AcceptEncoding.Add(new("gzip"));

		string requestString = await request.ToHttpStringAsync(token).ConfigureAwait(false);
		byte[] bytes = Encoding.UTF8.GetBytes(requestString);

		Stream transportStream = connection.GetTransportStream();

		try
		{
			await transportStream.WriteAsync(bytes.AsMemory(0, bytes.Length), token).ConfigureAwait(false);
			await transportStream.FlushAsync(token).ConfigureAwait(false);
		}
		catch (IOException e)
		{
			throw new TorConnectionWriteException("Could not use transport stream to write data.", e);
		}

		try
		{
			return await HttpResponseMessageExtensions.CreateNewAsync(transportStream, request.Method, token).ConfigureAwait(false);
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			throw new TorConnectionReadException("Could not read HTTP response.", e);
		}
	}

	/// <summary>Allows to report that a Tor circuit was closed.</summary>
	/// <param name="circuitName">Name of the circuit. Example is: <c>IK1DG1HZCZFEQUTF86O86</c>.</param>
	/// <remarks>
	/// Useful to clean up <see cref="ConnectionPerHost"/> so that we do not exhaust <see cref="MaxConnectionsPerHost"/> limit.
	/// <para>If client code forgets to dispose <see cref="PersonCircuit"/>, this should help us to recover eventually.</para>
	/// </remarks>
	public async Task ReportCircuitClosedAsync(string circuitName, CancellationToken cancellationToken)
	{
		using (await ObtainPoolConnectionLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach ((string host, List<TorTcpConnection> tcpConnections) in ConnectionPerHost)
			{
				foreach (TorTcpConnection tcpConnection in tcpConnections)
				{
					if (tcpConnection.Circuit.Name == circuitName)
					{
						tcpConnection.MarkAsToDispose(force: false);
					}
				}
			}
		}
	}

	private static string GetRequestHost(HttpRequestMessage request)
	{
		return Guard.NotNullOrEmptyOrWhitespace(nameof(request.RequestUri.DnsSafeHost), request.RequestUri!.DnsSafeHost, trim: true);
	}

	/// <summary>Gets reserved <see cref="TorTcpConnection"/> to use, if any.</summary>
	/// <param name="host">URI's host value.</param>
	/// <param name="circuit">Tor circuit for which to get a TCP connection.</param>
	/// <returns>Whether a connection can be added to <see cref="ConnectionPerHost"/> and reserved connection to use, if any.</returns>
	/// <remarks>Guarded by <see cref="ObtainPoolConnectionLock"/>.</remarks>
	private bool GetPoolConnectionNoLock(string host, ICircuit circuit, out TorTcpConnection? connection)
	{
		if (!ConnectionPerHost.ContainsKey(host))
		{
			ConnectionPerHost.Add(host, new());
		}

		List<TorTcpConnection> hostConnections = ConnectionPerHost[host];

		// Find TCP connections to dispose.
		foreach (TorTcpConnection tcpConnection in hostConnections.FindAll(c => c.NeedDisposal).ToList())
		{
			Logger.LogTrace($"['{tcpConnection}'] Connection is to be disposed.");
			hostConnections.Remove(tcpConnection);
			tcpConnection.Dispose();
		}

		// Find the first free TCP connection, if it exists.
		connection = hostConnections.Find(connection => (connection.Circuit == circuit) && connection.TryReserve());

		bool canBeAdded = hostConnections.Count < MaxConnectionsPerHost;

		if (!canBeAdded)
		{
			Logger.LogTrace($"Beware! Too many active connections: '{string.Join(", ", hostConnections.Select(c => c.ToString()))}'.");
		}

		return canBeAdded;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				foreach (List<TorTcpConnection> list in ConnectionPerHost.Values)
				{
					foreach (TorTcpConnection connection in list)
					{
						Logger.LogTrace($"Dispose connection: '{connection}'");
						connection.Dispose();
					}
				}
			}
			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Dispose of unmanaged resources.
		Dispose(true);
	}
}
