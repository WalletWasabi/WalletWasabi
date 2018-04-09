using System.Net.Http.Headers;

namespace MagicalCryptoWallet.Http.Models
{
    public class HttpResponseContentHeaders
	{
		public HttpResponseHeaders ResponseHeaders { get; set; }
		public HttpContentHeaders ContentHeaders { get; set; }
	}
}
