using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi;

/// <summary>
/// Factory class to get proper <see cref="IHttpClient"/> client which is set up based on user settings.
/// </summary>
public class WasabiHttpClientFactory : IWasabiHttpClientFactory, IAsyncDisposable
{
	/// <summary>
	/// Creates a new instance of the object.
	/// </summary>
	/// <param name="torEndPoint">If <c>null</c> then clearnet (not over Tor) is used, otherwise HTTP requests are routed through provided Tor endpoint.</param>
	public WasabiHttpClientFactory(EndPoint? torEndPoint, Func<Uri>? backendUriGetter, bool torControlAvailable = true)
	{
		HttpClient = CreateLongLivedHttpClient(automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Brotli);

		TorEndpoint = torEndPoint;
		BackendUriGetter = backendUriGetter;

		// Connecting to loopback's URIs cannot be done via Tor.
		if (TorEndpoint is { } && (BackendUriGetter is null || !BackendUriGetter().IsLoopback))
		{
			TorHttpPool = new(TorEndpoint, torControlAvailable);
			BackendHttpClient = new TorHttpClient(BackendUriGetter, TorHttpPool, Mode.DefaultCircuit);
		}
		else
		{
			BackendHttpClient = new ClearnetHttpClient(HttpClient, BackendUriGetter);
		}

		SharedWasabiClient = new(BackendHttpClient);
	}

	/// <summary>Tor SOCKS5 endpoint.</summary>
	/// <remarks>The property should be <c>private</c> when Tor refactoring is done.</remarks>
	public EndPoint? TorEndpoint { get; }

	/// <remarks>The property should be <c>private</c> when Tor refactoring is done.</remarks>
	public Func<Uri>? BackendUriGetter { get; }

	/// <summary>Whether Tor is enabled or disabled.</summary>
	[MemberNotNullWhen(returnValue: true, nameof(TorEndpoint))]
	public bool IsTorEnabled => TorEndpoint is not null;

	/// <summary>The .NET HTTP client to be used by <see cref="ClearnetHttpClient"/> instances.</summary>
	private HttpClient HttpClient { get; }

	/// <summary>Available only when Tor is enabled in User settings.</summary>
	public TorHttpPool? TorHttpPool { get; }

	/// <summary>Backend HTTP client, shared instance.</summary>
	private IHttpClient BackendHttpClient { get; }

	/// <summary>Shared instance of <see cref="WasabiClient"/>.</summary>
	public WasabiClient SharedWasabiClient { get; }

	/// <summary>
	/// Creates a long-lived <see cref="HttpClient"/> instance for accessing clearnet sites.
	/// </summary>
	/// <remarks>Created HTTP client handles correctly DNS changes.</remarks>
	/// <seealso href="https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory"/>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HTTP client will dispose the handler correctly.")]
	public static HttpClient CreateLongLivedHttpClient(TimeSpan? pooledConnectionLifetime = null, DecompressionMethods? automaticDecompression = null)
	{
		SocketsHttpHandler handler = new()
		{
			PooledConnectionLifetime = pooledConnectionLifetime ?? TimeSpan.FromMinutes(5),
		};

		if (automaticDecompression is not null)
		{
			handler.AutomaticDecompression = automaticDecompression.Value;
		}

		return new HttpClient(handler);
	}

	/// <summary>
	/// Creates new <see cref="TorHttpClient"/> or <see cref="ClearnetHttpClient"/> based on user settings.
	/// </summary>
	public IHttpClient NewHttpClient(Func<Uri>? baseUriFn, Mode mode, ICircuit? circuit = null, int maximumRedirects = 0)
	{
		// Connecting to loopback's URIs cannot be done via Tor.
		if (TorHttpPool is { } && (BackendUriGetter is null || !BackendUriGetter().IsLoopback))
		{
			return new TorHttpClient(baseUriFn, TorHttpPool, mode, circuit, maximumRedirects);
		}
		else
		{
			return new ClearnetHttpClient(HttpClient, baseUriFn);
		}
	}

	/// <summary>Creates new <see cref="TorHttpClient"/>.</summary>
	/// <remarks>Do not use this function unless <see cref="NewHttpClient(Func{Uri}?, Mode, ICircuit?, int)"/> is not sufficient for your use case.</remarks>
	/// <exception cref="InvalidOperationException"/>
	public TorHttpClient NewTorHttpClient(Mode mode, Func<Uri>? baseUriFn = null, ICircuit? circuit = null)
	{
		if (TorEndpoint is null)
		{
			throw new InvalidOperationException("Tor is not enabled in the user settings.");
		}

		return (TorHttpClient)NewHttpClient(baseUriFn, mode, circuit);
	}

	/// <summary>
	/// Creates a new <see cref="IHttpClient"/> with the base URI is set to Wasabi Backend.
	/// </summary>
	public IHttpClient NewHttpClient(Mode mode, ICircuit? circuit = null, int maximumRedirects = 0)
	{
		return NewHttpClient(BackendUriGetter, mode, circuit, maximumRedirects);
	}

	public async ValueTask DisposeAsync()
	{
		// Dispose managed state (managed objects).
		if (BackendHttpClient is IDisposable httpClient)
		{
			httpClient.Dispose();
		}

		HttpClient.Dispose();

		if (TorHttpPool is not null)
		{
			await TorHttpPool.DisposeAsync().ConfigureAwait(false);
		}
	}
}
