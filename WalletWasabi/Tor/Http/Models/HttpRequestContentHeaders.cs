using System.Net.Http.Headers;

namespace WalletWasabi.Tor.Http.Models
{
	public class HttpRequestContentHeaders
	{
		public HttpRequestHeaders RequestHeaders { get; set; }
		public HttpContentHeaders ContentHeaders { get; set; }
	}
}
