using System.Net.Http.Headers;

namespace WalletWasabi.Tor.Http.Models;

public record HttpRequestContentHeaders(
	HttpRequestHeaders RequestHeaders,
	HttpContentHeaders ContentHeaders);
