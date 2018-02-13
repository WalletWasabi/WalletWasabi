using MagicalCryptoWallet.Http.Models;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MagicalCryptoWallet.Http.Constants;

namespace System.Net.Http
{
	public static class HttpResponseMessageExtensions
    {
		public static async Task<HttpResponseMessage> CreateNewAsync(this HttpResponseMessage me, Stream responseStream, HttpMethod requestMethod)
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
			
			var position = 0;
			string startLine = await HttpMessageHelper.ReadStartLineAsync(responseStream).ConfigureAwait(false);
			position += startLine.Length;

			var statusLine = StatusLine.CreateNew(startLine);
			var response = new HttpResponseMessage(statusLine.StatusCode);

			string headers = await HttpMessageHelper.ReadHeadersAsync(responseStream).ConfigureAwait(false);
			position += headers.Length + 2;

			var headerSection = HeaderSection.CreateNew(headers);
			var headerStruct = headerSection.ToHttpResponseHeaders();

			HttpMessageHelper.AssertValidHeaders(headerStruct.ResponseHeaders, headerStruct.ContentHeaders);
			response.Content = await HttpMessageHelper.GetContentAsync(responseStream, headerStruct, requestMethod, statusLine).ConfigureAwait(false);

			HttpMessageHelper.CopyHeaders(headerStruct.ResponseHeaders, response.Headers);
			if (response.Content != null)
			{
				HttpMessageHelper.CopyHeaders(headerStruct.ContentHeaders, response.Content.Headers);
			}
			return response;
		}		

		public static async Task<Stream> ToStreamAsync(this HttpResponseMessage me)
		{
			return new MemoryStream(Encoding.UTF8.GetBytes(await me.ToHttpStringAsync().ConfigureAwait(false)));
		}
		public static async Task<string> ToHttpStringAsync(this HttpResponseMessage me)
		{
			var startLine = new StatusLine(new HttpProtocol($"HTTP/{me.Version.Major}.{me.Version.Minor}"), me.StatusCode).ToString();

			string headers = "";
			if (me.Headers != null && me.Headers.Count() != 0)
			{
				var headerSection = HeaderSection.CreateNew(me.Headers);
				headers += headerSection.ToString(endWithTwoCRLF: false);
			}

			string messageBody = "";
			if (me.Content != null)
			{
				if (me.Content.Headers != null && me.Content.Headers.Count() != 0)
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
