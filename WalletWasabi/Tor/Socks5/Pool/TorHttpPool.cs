using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Messages.StreamStatus;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Http.Models;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Socks5.Pool;

/// <summary>
/// The pool represents a set of multiple TCP connections to Tor SOCKS5 endpoint that are
/// stored in <see cref="TorTcpConnection"/>s.
/// </summary>
public class TorHttpPool : IAsyncDisposable
{
	/// <summary>Maximum number of <see cref="TorTcpConnection"/>s per URI host.</summary>
	/// <remarks>This parameter affects maximum parallelization for given URI host.</remarks>
	public const int MaxConnectionsPerHost = 1000;

	private static readonly StringWithQualityHeaderValue GzipEncoding = new("gzip");

	private static readonly UnboundedChannelOptions Options = new()
	{
		SingleWriter = false,
	};

	public TorHttpPool(EndPoint endpoint)
		: this(new TorTcpConnectionFactory(endpoint))
	{
	}

	/// <summary>Constructor that helps in tests.</summary>
	internal TorHttpPool(TorTcpConnectionFactory tcpConnectionFactory)
	{
		TcpConnectionFactory = tcpConnectionFactory;
		PreBuildingRequestChannel = Channel.CreateUnbounded<TorPrebuildCircuitRequest>(Options);
		PreBuildingLoopTask = Task.Run(PreBuildingLoopAsync);
	}

	private Task PreBuildingLoopTask { get; }
	private CancellationTokenSource LoopCts { get; } = new();

	/// <summary>Channel with pre-building requests.</summary>
	private Channel<TorPrebuildCircuitRequest> PreBuildingRequestChannel { get; }

	/// <summary>Key is always Tor SOCKS5 username. Value is the corresponding latest Tor stream update received over Tor control protocol.</summary>
	/// <remarks>All access to this object must be guarded by <see cref="ConnectionsLock"/>.</remarks>
	private Dictionary<string, TorStreamInfo?> TorStreamsBeingBuilt { get; } = new();

	/// <summary>Key is always a URI host. Value is a list of pool connections that can connect to the URI host.</summary>
	/// <remarks>All access to this object must be guarded by <see cref="ConnectionsLock"/>.</remarks>
	private Dictionary<string, List<TorTcpConnection>> ConnectionPerHost { get; } = new();

	/// <remarks>Lock object to guard <see cref="ConnectionPerHost"/> and <see cref="TorStreamsBeingBuilt"/>.</remarks>
	private object ConnectionsLock { get; } = new();

	private TorTcpConnectionFactory TcpConnectionFactory { get; }

	public static DateTimeOffset? TorDoesntWorkSince { get; private set; }

	public Task<bool> IsTorRunningAsync(CancellationToken cancel)
	{
		return TcpConnectionFactory.IsTorRunningAsync(cancel);
	}

	public static Exception? LatestTorException { get; private set; } = null;

	/// <summary>
	/// This method is called when an HTTP(s) request fails for some reason.
	/// <para>The information is stored to allow <see cref="TorMonitor"/> to restart Tor as deemed fit.</para>
	/// </summary>
	/// <param name="e">Tor exception.</param>
	private void OnTorRequestFailed(Exception e)
	{
		TorDoesntWorkSince ??= DateTimeOffset.UtcNow;

		if (e is HttpRequestException)
		{
			LatestTorException = e.InnerException is null ? e : e.InnerException;
		}
		else
		{
			LatestTorException = e;
		}
	}

	/// <inheritdoc cref="SendAsync(HttpRequestMessage, ICircuit, int, CancellationToken)"/>
	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, ICircuit circuit, CancellationToken cancellationToken)
		=> SendAsync(request, circuit, maximumRedirects: 0, cancellationToken);

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
	/// <para><see cref="ConnectionsLock"/> is acquired only for <see cref="TorTcpConnection"/> selection.</para>
	/// </summary>
	/// <param name="maximumRedirects"><c>0</c> to disable redirecting altogether, otherwise a maximum allowed number of hops.</param>
	/// <exception cref="HttpRequestException">When <paramref name="request"/> fails to be processed.</exception>
	/// <exception cref="OperationCanceledException">When the operation was canceled.</exception>
	public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, ICircuit circuit, int maximumRedirects, CancellationToken cancellationToken)
	{
		int i = 0;
		int attemptsNo = 3;
		TorTcpConnection? connection = null;

		try
		{
			Uri requestUriOverride = request.RequestUri!;

			do
			{
				i++;
				TorTcpConnection? connectionToDispose = null;
				OneOffCircuit? oneOffCircuitToDispose = null;

				INamedCircuit? namedCircuit = null;

				if (circuit is AnyOneOffCircuit)
				{
					oneOffCircuitToDispose = new();
					namedCircuit = oneOffCircuitToDispose;
				}
				else if (circuit is INamedCircuit withName)
				{
					namedCircuit = withName;
				}
				else
				{
					throw new NotImplementedException("This should never happen.");
				}

				bool attemptSuccessful = false;

				try
				{
					connection = await ObtainFreeConnectionAsync(requestUriOverride, namedCircuit, cancellationToken).ConfigureAwait(false);
					connectionToDispose = connection;

					Logger.LogTrace($"['{connection}'][Attempt #{i}] About to send request.");
					HttpResponseMessage response = await SendCoreAsync(connection, request, requestUriOverride, cancellationToken).ConfigureAwait(false);

					// Client works OK, no need to dispose.
					connectionToDispose = null;

					// Let others use the client.
					TcpConnectionState state = connection.Unreserve();
					Logger.LogTrace($"['{connection}'][Attempt #{i}] Unreserve. State is: '{state}'.");

					attemptSuccessful = true;
					TorDoesntWorkSince = null;
					LatestTorException = null;

					// See https://github.com/dotnet/runtime/blob/47071da67320985a10f4b70f50f894ab411f4994/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/RedirectHandler.cs#L91-L96.
					if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.MultipleChoices or HttpStatusCode.PermanentRedirect)
					{
						if (maximumRedirects > 0)
						{
							maximumRedirects--;
							requestUriOverride = GetUriForRedirect(response, requestUriOverride);

							// Do not return response now, but try again with the new request URI.
							continue;
						}

						Logger.LogDebug($"['{connection}'][Attempt #{i}] Redirect limit reached.");
					}

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
					Logger.LogTrace($"['{connection}'] Circuit '{namedCircuit.Name}' has expired and cannot be used again.", e);

					throw new HttpRequestException($"Circuit '{namedCircuit.Name}' has expired and cannot be used again.", e);
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
				catch (TorConnectionException e)
				{
					Logger.LogTrace($"['{connection}'] Tor SOCKS5 connection failed.", e);

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
				catch (OperationCanceledException)
				{
					throw;
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

					// If our request failed but other requests seem to work, try a different circuit.
					if (!attemptSuccessful && TorDoesntWorkSince is null)
					{
						namedCircuit.IncrementIsolationId();
					}

					oneOffCircuitToDispose?.Dispose();
				}
			}
			while (i < attemptsNo);
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace($"['{connection}'] Request was canceled: '{request.RequestUri}'.");
			throw;
		}
		catch (Exception e)
		{
			Logger.LogTrace($"['{connection}'] Request failed with exception", e);
			OnTorRequestFailed(e);
			throw;
		}

		throw new NotImplementedException("This should never happen.");
	}

	private static Uri GetUriForRedirect(HttpResponseMessage response, Uri currentUri)
	{
		if (!response.Headers.TryGetValues("location", out IEnumerable<string>? locations))
		{
			throw new HttpRequestException("'location' HTTP header is missing.");
		}

		Uri result = new(locations.Last());

		// Ensure the redirect location is an absolute URI.
		if (!result.IsAbsoluteUri)
		{
			result = new Uri(currentUri, result);
		}

		// Per https://tools.ietf.org/html/rfc7231#section-7.1.2, a redirect location without a fragment should
		// inherit the fragment from the original URI.
		string requestFragment = currentUri.Fragment;
		if (!string.IsNullOrEmpty(requestFragment))
		{
			string redirectFragment = result.Fragment;
			if (string.IsNullOrEmpty(redirectFragment))
			{
				result = new UriBuilder(result) { Fragment = requestFragment }.Uri;
			}
		}

		string debugMessage = (locations.Count() > 1)
			? $"Multiple 'location' headers found for '{currentUri}'."
			: $"Redirecting '{currentUri}' to '{result}'.";

		Logger.LogDebug(debugMessage);

		return result;
	}

	private async Task<TorTcpConnection> ObtainFreeConnectionAsync(Uri requestUri, ICircuit circuit, CancellationToken token)
	{
		Logger.LogTrace($"> request='{requestUri}', circuit={circuit}");

		DateTime start = DateTime.UtcNow;
		string host = GetRequestHost(requestUri);

		do
		{
			bool canBeAdded;
			TorTcpConnection? connection;

			lock (ConnectionsLock)
			{
				canBeAdded = GetPoolConnectionNoLock(host, circuit, out connection);

				if (connection is not null)
				{
					Logger.LogTrace($"[OLD {connection}]['{requestUri}'] Re-use existing Tor SOCKS5 connection.");
					return connection;
				}
			}

			OneOffCircuit? oneOffCircuitToDispose = null;

			INamedCircuit namedCircuit;

			if (circuit is AnyOneOffCircuit)
			{
				oneOffCircuitToDispose = new OneOffCircuit();
				namedCircuit = oneOffCircuitToDispose;
			}
			else
			{
				namedCircuit = (INamedCircuit)circuit;
			}

			try
			{
				// The circuit may be disposed almost immediately after this check but we don't mind.
				if (!namedCircuit.IsActive)
				{
					throw new TorCircuitExpiredException();
				}

				if (canBeAdded)
				{
					connection = await CreateNewConnectionAsync(requestUri, namedCircuit, token).ConfigureAwait(false);

					// Do not dispose.
					oneOffCircuitToDispose = null;

					if (connection is not null)
					{
						DateTime end = DateTime.UtcNow;
						Logger.LogTrace($"[NEW {connection}]['{requestUri}'][{(end - start).TotalSeconds:0.##s}] Using new Tor SOCKS5 connection.");
						return connection;
					}
				}
			}
			catch (TorException)
			{
				namedCircuit.IncrementIsolationId();
				throw;
			}
			finally
			{
				oneOffCircuitToDispose?.Dispose();
			}

			Logger.LogTrace("Wait 1s for a free pool connection.");
			await Task.Delay(1000, token).ConfigureAwait(false);
		}
		while (true);
	}

	private async Task<TorTcpConnection?> CreateNewConnectionAsync(Uri requestUri, INamedCircuit circuit, CancellationToken cancellationToken)
	{
		lock (ConnectionsLock)
		{
			TorStreamsBeingBuilt[circuit.Name] = null;
		}

		TorTcpConnection? connection = null;

		try
		{
			connection = await TcpConnectionFactory.ConnectAsync(requestUri, circuit, cancellationToken).ConfigureAwait(false);
			Logger.LogTrace($"[NEW {connection}]['{requestUri}'] Created new Tor SOCKS5 connection.");
		}
		catch (TorConnectCommandFailedException e) when (e.RepField == RepField.TtlExpired)
		{
			Logger.LogTrace($"['{requestUri}'] TTL expired occurred. Probably some relay/networking problem. No need to worry too much.");
			throw;
		}
		catch (TorException e)
		{
			Logger.LogTrace($"['{requestUri}'][ERROR] Failed to create a new pool connection.", e);
			throw;
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace($"['{requestUri}'] Operation was canceled.");
			throw;
		}
		catch (Exception e)
		{
			Logger.LogTrace($"['{requestUri}'][EXCEPTION] {e}");
			throw;
		}
		finally
		{
			lock (ConnectionsLock)
			{
				if (TorStreamsBeingBuilt.Remove(circuit.Name, out TorStreamInfo? streamInfo))
				{
					if (connection is not null && streamInfo is not null)
					{
						connection.SetLatestStreamInfo(streamInfo, ifEmpty: true);
					}
				}

				if (connection is not null)
				{
					string host = GetRequestHost(requestUri);

					if (!ConnectionPerHost.ContainsKey(host))
					{
						ConnectionPerHost.Add(host, new List<TorTcpConnection>());
					}

					ConnectionPerHost[host].Add(connection);
				}
			}
		}

		Logger.LogTrace($"< connection='{connection}'");
		return connection;
	}

	/// <param name="requestUriOverride">URI that should be used instead of <paramref name="request"/>'s request URI. Useful for HTTP redirect support.</param>
	/// <exception cref="TorConnectionWriteException">When a failure during sending our HTTP(s) request to Tor SOCKS5 occurs.</exception>
	/// <exception cref="TorConnectionReadException">When a failure during receiving HTTP response from Tor SOCKS5 occurs.</exception>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	internal virtual async Task<HttpResponseMessage> SendCoreAsync(TorTcpConnection connection, HttpRequestMessage request, Uri requestUriOverride, CancellationToken token)
	{
		// https://tools.ietf.org/html/rfc7230#section-2.6
		// Intermediaries that process HTTP messages (i.e., all intermediaries
		// other than those acting as tunnels) MUST send their own HTTP - version
		// in forwarded messages.
		request.Version = HttpProtocol.HTTP11.Version;

		// Do not re-add the header if it is already present.
		if (!request.Headers.AcceptEncoding.Contains(GzipEncoding))
		{
			request.Headers.AcceptEncoding.Add(GzipEncoding);
		}

		string requestString = await request.ToHttpStringAsync(requestUriOverride, token).ConfigureAwait(false);
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

	/// <summary>
	/// Allows to report that a Tor stream status changed.
	/// </summary>
	/// <param name="streamUsername">Username of the Tor stream. Example is: <c>IK1DG1HZCZFEQUTF86O86</c>.</param>
	/// <param name="circuitID">Tor circuit ID for logging purposes. Example is: <c>35</c>.</param>
	/// <remarks>
	/// Useful to clean up <see cref="ConnectionPerHost"/> so that we do not exhaust <see cref="MaxConnectionsPerHost"/> limit.
	/// <para>If client code forgets to dispose <see cref="PersonCircuit"/>, this should help us to recover eventually.</para>
	/// </remarks>
	public void ReportStreamStatus(string streamUsername, StreamStatusFlag streamStatus, string circuitID)
	{
		lock (ConnectionsLock)
		{
			// If the key is not present, then are not the ones starting that Tor stream.
			if (TorStreamsBeingBuilt.ContainsKey(streamUsername))
			{
				// We can receive multiple such reports potentially. We don't mind.
				TorStreamsBeingBuilt[streamUsername] = new TorStreamInfo(circuitID, streamStatus);
			}

			TorTcpConnection? tcpConnection = null;

			// Find our TCP connection based on username we provided when connecting to Tor SOCKS5.
			foreach ((string host, List<TorTcpConnection> tcpConnections) in ConnectionPerHost)
			{
				tcpConnection = tcpConnections.FirstOrDefault(connection => connection.Circuit.Name == streamUsername);

				if (tcpConnection is not null)
				{
					break;
				}
			}

			if (tcpConnection is not null)
			{
				tcpConnection.SetLatestStreamInfo(new TorStreamInfo(circuitID, streamStatus));

				if (streamStatus is StreamStatusFlag.CLOSED or StreamStatusFlag.FAILED)
				{
					Logger.LogTrace($"Tor circuit was closed: #{circuitID} ('{tcpConnection.Circuit}').");
					tcpConnection.MarkAsToDispose(force: false);
				}
			}
		}
	}

	private static string GetRequestHost(Uri requestUri)
	{
		return Guard.NotNullOrEmptyOrWhitespace(nameof(requestUri.DnsSafeHost), requestUri!.DnsSafeHost, trim: true);
	}

	/// <summary>Gets reserved <see cref="TorTcpConnection"/> to use, if any.</summary>
	/// <param name="host">URI's host value.</param>
	/// <param name="circuit">Tor circuit for which to get a TCP connection.</param>
	/// <returns>Whether a connection can be added to <see cref="ConnectionPerHost"/> and reserved connection to use, if any.</returns>
	/// <remarks>Guarded by <see cref="ConnectionsLock"/>.</remarks>
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
		connection = hostConnections.Find(connection =>
		{
			// One-off circuits are fungible. They are not compared by reference as other circuits are.
			if (circuit is AnyOneOffCircuit)
			{
				return (connection.Circuit is OneOffCircuit) && connection.TryReserve();
			}

			return (connection.Circuit == circuit) && connection.TryReserve();
		});

		bool canBeAdded = hostConnections.Count < MaxConnectionsPerHost;

		if (!canBeAdded)
		{
			Logger.LogTrace($"Beware! Too many active connections: '{string.Join(", ", hostConnections.Select(c => c.ToString()))}'.");
		}

		return canBeAdded;
	}

	/// <summary>
	/// Loop that keeps handling requests for pre-building new Tor circuits and that keeps processing the existing requests.
	/// </summary>
	/// <remarks>Alternative (albeit probably harder) approach would be to use <c>EXTENDCIRCUIT 0</c> Tor control command to create the circuits directly.</remarks>
	/// <seealso href="https://github.com/torproject/torspec/blob/833d6b27a4427b5bbb2189218c10fd568fc3e415/control-spec.txt#L1284"/>
	private async Task PreBuildingLoopAsync()
	{
		ConcurrentDictionary<long, ICircuit> prebuildingTasks = new();

		try
		{
			long counter = 0;
			CancellationToken cancellationToken = LoopCts.Token;

			while (!cancellationToken.IsCancellationRequested)
			{
				TorPrebuildCircuitRequest request = await PreBuildingRequestChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
				long i = ++counter;

				Task task = Task.Run(
					async () =>
					{
						OneOffCircuit circuit = new();
						prebuildingTasks.TryAdd(i, circuit);

						try
						{
							Logger.LogTrace($"[{i}][{circuit.Name}] Wait {request.RandomDelay} before pre-building.");
							await Task.Delay(request.RandomDelay, cancellationToken).ConfigureAwait(false);

							Logger.LogTrace($"[{i}][{circuit.Name}] Start pre-building the Tor circuit.");
							Stopwatch sw = Stopwatch.StartNew();

							// Not to be disposed now.
							TorTcpConnection? _ = await CreateNewConnectionAsync(request.BaseUri, circuit, cancellationToken).ConfigureAwait(false);
							sw.Stop();

							Logger.LogTrace($"[{i}][{circuit.Name}] Tor circuit built in {sw.ElapsedMilliseconds} ms.");
						}
						catch (OperationCanceledException)
						{
							Logger.LogDebug("Operation was cancelled.");
						}
						catch (Exception e)
						{
							Logger.LogError(e);
						}
						finally
						{
							prebuildingTasks.TryRemove(i, out _);
						}
					},
				cancellationToken);
			}

			Logger.LogDebug("Circuit pre-building loop gracefully terminated.");
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Circuit pre-building loop was stopped by user.");
		}
		catch (Exception e)
		{
			// This is an unrecoverable issue.
			Logger.LogError($"Exception occurred in the pre-building loop: {e}.");
			throw;
		}
	}

	/// <summary>
	/// Makes sure there are <paramref name="count"/> connections in <paramref name="deadline"/> time span.
	/// </summary>
	/// <param name="baseUri">Host name for which to fire up a new Tor circuit.</param>
	/// <param name="count">Number of <see cref="OneOffCircuit"/> Tor circuits to create.</param>
	/// <param name="deadline">Time span during which all Tor circuits should be build.</param>
	public void PrebuildCircuitsUpfront(Uri baseUri, int count, TimeSpan deadline)
	{
		Logger.LogTrace($"> baseUri='{baseUri}', count={count}, deadline={deadline}");

		for (int i = 1; i <= count; i++)
		{
			TimeSpan randomDelay = TimeSpan.FromMilliseconds(SecureRandom.Instance.GetInt(0, (int)deadline.TotalMilliseconds));

			TorPrebuildCircuitRequest request = new(baseUri, randomDelay);
			if (!PreBuildingRequestChannel.Writer.TryWrite(request))
			{
				Logger.LogDebug($"Failed to register all pre-building requests. Failed request: '{request}'.");
				break;
			}
		}

		Logger.LogTrace("<");
	}

	public async ValueTask DisposeAsync()
	{
		foreach (List<TorTcpConnection> list in ConnectionPerHost.Values)
		{
			foreach (TorTcpConnection connection in list)
			{
				Logger.LogTrace($"Dispose connection: '{connection}'");
				connection.Dispose();
			}
		}

		// Stop the loop.
		LoopCts.Cancel();

		try
		{
			// Wait until the loop stops.
			await PreBuildingLoopTask.ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Logger.LogDebug("Unexpected issue in stopping the pre-building loop.", e);
		}

		LoopCts.Dispose();
	}
}
