using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http.Models;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Extensions
{
	public static class HttpRequestMessageExtensions
	{
		public static async Task<string> ToHttpStringAsync(this HttpRequestMessage request)
		{
			// https://tools.ietf.org/html/rfc7230#section-5.4
			// The "Host" header field in a request provides the host and port
			// information from the target URI, enabling the origin server to
			// distinguish among resources while servicing requests for multiple
			// host names on a single IP address.
			// Host = uri - host[":" port] ; Section 2.7.1
			// A client MUST send a Host header field in all HTTP/1.1 request messages.
			if (request.Method != new HttpMethod("CONNECT"))
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

			var startLine = new RequestLine(request.Method, request.RequestUri!, new HttpProtocol($"HTTP/{request.Version.Major}.{request.Version.Minor}")).ToString();

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

				messageBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			return startLine + headers + CRLF + messageBody;
		}
	}
}
