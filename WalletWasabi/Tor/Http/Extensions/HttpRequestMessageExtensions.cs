using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Tor.Http.Models;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Extensions;

public static class HttpRequestMessageExtensions
{
	private static readonly HttpMethod ConnectHttpMethod = new("CONNECT");

	/// <summary>
	/// Converts <see cref="HttpRequestMessage"/> to plaintext HTTP request string.
	/// </summary>
	/// <param name="request">HTTP request to convert.</param>
	/// <returns>String representation of <paramref name="request"/> according to <seealso href="https://tools.ietf.org/html/rfc7230"/>.</returns>
	public static async Task<string> ToHttpStringAsync(this HttpRequestMessage request, CancellationToken cancellationToken = default)
	{
		// https://tools.ietf.org/html/rfc7230#section-3.3.2
		// A user agent SHOULD send a Content-Length in a request message when no Transfer-Encoding is sent and the request method defines a meaning
		// for an enclosed payload body. For example, a Content-Length header field is normally sent in a POST request even when the value is 0
		// (indicating an empty payload body). A user agent SHOULD NOT send a Content-Length header field when the request message does not contain
		// a payload body and the method semantics do not anticipate such a body.
		if (request.Method == HttpMethod.Post)
		{
			if (request.Headers.TransferEncoding.Count == 0)
			{
				if (request.Content is null)
				{
					request.Content = new ByteArrayContent(Array.Empty<byte>()); // dummy empty content
					request.Content.Headers.ContentLength = 0;
				}
				else
				{
					request.Content.Headers.ContentLength ??= (await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Length;
				}
			}
		}

		// https://tools.ietf.org/html/rfc7230#section-5.4
		// The "Host" header field in a request provides the host and port information from the target URI, enabling the origin server to
		// distinguish among resources while servicing requests for multiple host names on a single IP address.
		// Host = uri - host[":" port] ; Section 2.7.1
		// A client MUST send a Host header field in all HTTP/1.1 request messages.
		if (request.Method != ConnectHttpMethod)
		{
			if (!request.Headers.Contains("Host"))
			{
				// https://tools.ietf.org/html/rfc7230#section-5.4
				// If the target URI includes an authority component, then a
				// client MUST send a field-value for Host that is identical to that
				// authority component, excluding any userinfo subcomponent and its "@"
				// delimiter(Section 2.7.1).If the authority component is missing or
				// undefined for the target URI, then a client MUST send a Host header
				// field with an empty field - value.
				request.Headers.TryAddWithoutValidation("Host", request.RequestUri!.Authority);
			}
		}

		string startLine = new RequestLine(request.Method, request.RequestUri!, new HttpProtocol($"HTTP/{request.Version.Major}.{request.Version.Minor}")).ToString();

		string headers = "";
		if (request.Headers.NotNullAndNotEmpty())
		{
			var headerSection = HeaderSection.CreateNew(request.Headers);
			headers += headerSection.ToString(endWithTwoCRLF: false);
		}

		string messageBody = "";
		if (request.Content is { })
		{
			if (request.Content.Headers.NotNullAndNotEmpty())
			{
				var headerSection = HeaderSection.CreateNew(request.Content.Headers);
				headers += headerSection.ToString(endWithTwoCRLF: false);
			}

			messageBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		return startLine + headers + CRLF + messageBody;
	}
}
