using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Http.Models;
using static WalletWasabi.Http.Constants;

namespace System.Net.Http
{
	public static class HttpRequestMessageExtensions
	{
		public static async Task<HttpRequestMessage> CreateNewAsync(Stream requestStream, CancellationToken ctsToken = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-3
			// The normal procedure for parsing an HTTP message is to read the
			// start - line into a structure, read each header field into a hash table
			// by field name until the empty line, and then use the parsed data to
			// determine if a message body is expected.If a message body has been
			// indicated, then it is read as a stream until an amount of octets
			// equal to the message body length is read or the connection is closed.

			// https://tools.ietf.org/html/rfc7230#section-3
			// All HTTP/ 1.1 messages consist of a start - line followed by a sequence
			// of octets in a format similar to the Internet Message Format
			// [RFC5322]: zero or more header fields(collectively referred to as
			// the "headers" or the "header section"), an empty line indicating the
			// end of the header section, and an optional message body.
			// HTTP - message = start - line
			//					* (header - field CRLF )
			//					CRLF
			//					[message - body]

			string startLine = await HttpMessageHelper.ReadStartLineAsync(requestStream, ctsToken);

			var requestLine = await RequestLine.CreateNewAsync(startLine);
			var request = new HttpRequestMessage(requestLine.Method, requestLine.URI);

			string headers = await HttpMessageHelper.ReadHeadersAsync(requestStream, ctsToken);

			var headerSection = await HeaderSection.CreateNewAsync(headers);
			var headerStruct = headerSection.ToHttpRequestHeaders();

			HttpMessageHelper.AssertValidHeaders(headerStruct.RequestHeaders, headerStruct.ContentHeaders);
			byte[] contentBytes = await HttpMessageHelper.GetContentBytesAsync(requestStream, headerStruct, ctsToken);
			contentBytes = HttpMessageHelper.HandleGzipCompression(headerStruct.ContentHeaders, contentBytes);
			request.Content = contentBytes is null ? null : new ByteArrayContent(contentBytes);

			HttpMessageHelper.CopyHeaders(headerStruct.RequestHeaders, request.Headers);
			if (request.Content != null)
			{
				HttpMessageHelper.CopyHeaders(headerStruct.ContentHeaders, request.Content.Headers);
			}
			return request;
		}

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
			if (me.Content != null)
			{
				if (me.Content.Headers.NotNullAndNotEmpty())
				{
					var headerSection = HeaderSection.CreateNew(me.Content.Headers);
					headers += headerSection.ToString(endWithTwoCRLF: false);
				}

				messageBody = await me.Content.ReadAsStringAsync();
			}

			return startLine + headers + CRLF + messageBody;
		}

		public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage me)
		{
			var newMessage = new HttpRequestMessage(me.Method, me.RequestUri)
			{
				Version = me.Version
			};

			foreach (var header in me.Headers)
			{
				newMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			if (me.Content is null)
			{
				return newMessage;
			}

			var ms = new MemoryStream();
			await me.Content.CopyToAsync(ms);
			ms.Position = 0;
			var newContent = new StreamContent(ms);

			foreach (var header in me.Content.Headers)
			{
				newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			newMessage.Content = newContent;

			return newMessage;
		}
	}
}
