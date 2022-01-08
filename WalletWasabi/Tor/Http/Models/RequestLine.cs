using System.Net.Http;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Models;

// https://tools.ietf.org/html/rfc7230#section-3.1.1
// request-line   = method SP request-target SP HTTP-version CRLF
public class RequestLine : StartLine
{
	public RequestLine(HttpMethod method, Uri uri, HttpProtocol protocol) : base(protocol)
	{
		Method = method;

		// https://tools.ietf.org/html/rfc7230#section-2.7.1
		// A sender MUST NOT generate an "http" URI with an empty host identifier.
		if (string.IsNullOrEmpty(uri.DnsSafeHost))
		{
			throw new HttpRequestException("Host identifier is empty.");
		}

		URI = uri;
	}

	public HttpMethod Method { get; }
	public Uri URI { get; }

	public override string ToString()
	{
		return $"{Method.Method}{SP}{URI.AbsolutePath}{URI.Query}{SP}{Protocol}{CRLF}";
	}
}
