using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static WalletWasabi.Http.Constants;

namespace WalletWasabi.Http.Models
{
	// https://tools.ietf.org/html/rfc7230#section-3.1.1
	// request-line   = method SP request-target SP HTTP-version CRLF
	public class RequestLine : StartLine
	{
		public HttpMethod Method { get; }
		public Uri URI { get; }

		public RequestLine(HttpMethod method, Uri uri, HttpProtocol protocol) : base(protocol)
		{
			Method = method;
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			if (uri.DnsSafeHost == "")
			{
				throw new HttpRequestException("Host identifier is empty.");
			}

			URI = uri;
		}

		public override string ToString()
		{
			return $"{Method.Method}{SP}{URI.AbsolutePath}{URI.Query}{SP}{Protocol}{CRLF}";
		}

		public static RequestLine Parse(string requestLineString)
		{
			try
			{
				var parts = GetParts(requestLineString);
				var methodString = parts[0];
				var uri = new Uri(parts[1]);
				var protocolString = parts[2];

				var method = new HttpMethod(methodString);
				var protocol = new HttpProtocol(protocolString);

				return new RequestLine(method, uri, protocol);
			}
			catch (Exception ex)
			{
				throw new NotSupportedException($"Invalid {nameof(RequestLine)}.", ex);
			}
		}
	}
}
