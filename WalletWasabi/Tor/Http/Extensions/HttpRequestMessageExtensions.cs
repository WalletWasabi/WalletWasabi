using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http.Models;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Extensions
{
	public static class HttpRequestMessageExtensions
	{
		public static async Task<string> ToHttpStringAsync(this HttpRequestMessage me)
		{
			// https://tools.ietf.org/html/rfc7230#section-5.4
			// The "Host" header field in a request provides the host and port
			// information from the target URI, enabling the origin server to
			// distinguish among resources while servicing requests for multiple
			// host names on a single IP address.
			// Host = uri - host[":" port] ; Section 2.7.1
			// A client MUST send a Host header field in all HTTP/1.1 request messages.
			if (me.Method != new HttpMethod("CONNECT"))
			{
				if (!me.Headers.Contains("Host"))
				{
					// https://tools.ietf.org/html/rfc7230#section-5.4
					// If the target URI includes an authority component, then a
					// client MUST send a field-value for Host that is identical to that
					// authority component, excluding any userinfo subcomponent and its "@"
					// delimiter(Section 2.7.1).If the authority component is missing or
					// undefined for the target URI, then a client MUST send a Host header
					// field with an empty field - value.
					me.Headers.TryAddWithoutValidation("Host", me.RequestUri.Authority);
				}
			}

			var startLine = new RequestLine(me.Method, me.RequestUri, new HttpProtocol($"HTTP/{me.Version.Major}.{me.Version.Minor}")).ToString();

			string headers = "";
			if (me.Headers.NotNullAndNotEmpty())
			{
				var headerSection = HeaderSection.CreateNew(me.Headers);
				headers += headerSection.ToString(endWithTwoCRLF: false);
			}

			string messageBody = "";
			if (me.Content is { })
			{
				if (me.Content.Headers.NotNullAndNotEmpty())
				{
					var headerSection = HeaderSection.CreateNew(me.Content.Headers);
					headers += headerSection.ToString(endWithTwoCRLF: false);
				}

				messageBody = await me.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			return startLine + headers + CRLF + messageBody;
		}
	}
}
