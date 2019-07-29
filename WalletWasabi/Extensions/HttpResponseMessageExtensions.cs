using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Http.Models;
using static WalletWasabi.Http.Constants;

namespace System.Net.Http
{
	public static class HttpResponseMessageExtensions
	{
		public static async Task<HttpResponseMessage> CreateNewAsync(Stream responseStream, HttpMethod requestMethod)
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

			string startLine = await HttpMessageHelper.ReadStartLineAsync(responseStream);

			var statusLine = StatusLine.CreateNew(startLine);
			var response = new HttpResponseMessage(statusLine.StatusCode);

			string headers = await HttpMessageHelper.ReadHeadersAsync(responseStream);

			var headerSection = HeaderSection.CreateNew(headers);
			var headerStruct = headerSection.ToHttpResponseHeaders();

			HttpMessageHelper.AssertValidHeaders(headerStruct.ResponseHeaders, headerStruct.ContentHeaders);
			byte[] contentBytes = await HttpMessageHelper.GetContentBytesAsync(responseStream, headerStruct, requestMethod, statusLine);
			contentBytes = HttpMessageHelper.HandleGzipCompression(headerStruct.ContentHeaders, contentBytes);
			response.Content = contentBytes is null ? null : new ByteArrayContent(contentBytes);

			HttpMessageHelper.CopyHeaders(headerStruct.ResponseHeaders, response.Headers);
			if (response.Content != null)
			{
				HttpMessageHelper.CopyHeaders(headerStruct.ContentHeaders, response.Content.Headers);
			}
			return response;
		}

		public static async Task<Stream> ToStreamAsync(this HttpResponseMessage me)
		{
			return new MemoryStream(Encoding.UTF8.GetBytes(await me.ToHttpStringAsync()));
		}

		public static async Task<string> ToHttpStringAsync(this HttpResponseMessage me)
		{
			var startLine = new StatusLine(new HttpProtocol($"HTTP/{me.Version.Major}.{me.Version.Minor}"), me.StatusCode).ToString();

			var headers = "";
			if (me.Headers.NotNullAndNotEmpty())
			{
				var headerSection = HeaderSection.CreateNew(me.Headers);
				headers += headerSection.ToString(endWithTwoCRLF: false);
			}

			var messageBody = "";
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

		public static async Task ThrowRequestExceptionFromContentAsync(this HttpResponseMessage me)
		{
			var error = await me.Content.ReadAsJsonAsync<string>();
			string errorMessage = error is null ? string.Empty : $"\n{error}";
			throw new HttpRequestException($"{me.StatusCode.ToReasonString()}{errorMessage}");
		}
	}
}
