using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Http;

public class TorHttpClient : IHttpClient
{
	/// <summary>Use this constructor when you want to issue relative or absolute HTTP requests.</summary>
	public TorHttpClient(Uri baseUri, TorHttpPool torHttpPool, Mode mode = Mode.DefaultCircuit, int maximumRedirects = 0) :
		this(() => baseUri, torHttpPool, mode, maximumRedirects: maximumRedirects)
	{
	}

	/// <summary>Use this constructor when you want to issue relative or absolute HTTP requests.</summary>
	public TorHttpClient(Func<Uri>? baseUriGetter, TorHttpPool torHttpPool, Mode mode = Mode.DefaultCircuit, ICircuit? circuit = null, int maximumRedirects = 0)
	{
		BaseUriGetter = baseUriGetter;
		TorHttpPool = torHttpPool;
		Mode = mode;
		MaximumRedirects = maximumRedirects;

		if (mode == Mode.SingleCircuitPerLifetime && circuit is null)
		{
			throw new NotSupportedException("Circuit is required in this case.");
		}

		PredefinedCircuit = mode switch
		{
			Mode.DefaultCircuit => DefaultCircuit.Instance,
			Mode.SingleCircuitPerLifetime => circuit,
			Mode.NewCircuitPerRequest => null,
			_ => throw new NotSupportedException(),
		};
	}

	public Func<Uri>? BaseUriGetter { get; }

	/// <summary>Whether each HTTP(s) request should use a separate Tor circuit or not to increase privacy.</summary>
	public Mode Mode { get; }

	/// <summary><c>0</c> to disable redirecting altogether, otherwise a maximum allowed number of hops.</summary>
	public int MaximumRedirects { get; }

	/// <summary>Non-null for <see cref="Mode.DefaultCircuit"/> and <see cref="Mode.SingleCircuitPerLifetime"/>.</summary>
	private ICircuit? PredefinedCircuit { get; }

	private TorHttpPool TorHttpPool { get; }

	/// <exception cref="HttpRequestException">When HTTP request fails to be processed. Inner exception may be an instance of <see cref="TorException"/>.</exception>
	/// <exception cref="OperationCanceledException">When <paramref name="cancellationToken"/> is canceled by the user.</exception>
	/// <inheritdoc cref="SendAsync(HttpRequestMessage, CancellationToken)"/>
	public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancellationToken = default)
	{
		if (BaseUriGetter is null)
		{
			throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
		}

		Uri requestUri = new(BaseUriGetter(), relativeUri);
		using HttpRequestMessage request = new(method, requestUri);

		if (content is { })
		{
			request.Content = content;
		}

		return await SendAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <exception cref="HttpRequestException">When <paramref name="request"/> fails to be processed.</exception>
	/// <exception cref="OperationCanceledException">If <paramref name="cancellationToken"/> is set.</exception>
	/// <remarks>
	/// No exception is thrown when the status code of the <see cref="HttpResponseMessage">response</see>
	/// is, for example, <see cref="HttpStatusCode.NotFound"/>.
	/// </remarks>
	public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (Mode is Mode.NewCircuitPerRequest)
		{
			return await TorHttpPool.SendAsync(request, AnyOneOffCircuit.Instance, MaximumRedirects, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			return await TorHttpPool.SendAsync(request, PredefinedCircuit!, MaximumRedirects, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc cref="TorHttpPool.PrebuildCircuitsUpfront(Uri, int, TimeSpan)"/>
	/// <exception cref="InvalidOperationException">When no <see cref="BaseUriGetter"/> is set.</exception>
	public void PrebuildCircuitsUpfront(int count, TimeSpan deadline)
	{
		if (BaseUriGetter is null)
		{
			throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
		}

		TorHttpPool.PrebuildCircuitsUpfront(BaseUriGetter(), count, deadline);
	}

	public Task<bool> IsTorRunningAsync(CancellationToken cancellationToken)
	{
		return TorHttpPool.IsTorRunningAsync(cancellationToken);
	}
}
