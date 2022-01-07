using System.Net.Http.Headers;

namespace WalletWasabi.Tor.Http.Models;

public record HttpResponseContentHeaders(
	HttpResponseHeaders ResponseHeaders,
	HttpContentHeaders ContentHeaders);
