using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Http.Models;
using static WalletWasabi.Http.Constants;

namespace System.Net.Http
{
	public static class HttpMessageHelper
	{
		public static async Task<string> ReadStartLineAsync(Stream stream, CancellationToken ctsToken = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-3
			// A recipient MUST parse an HTTP message as a sequence of octets in an
			// encoding that is a superset of US-ASCII[USASCII].

			// Read until the first CRLF
			// the CRLF is part of the startLine
			// https://tools.ietf.org/html/rfc7230#section-3.5
			// Although the line terminator for the start-line and header fields is
			// the sequence CRLF, a recipient MAY recognize a single LF as a line
			// terminator and ignore any preceding CR.
			var bab = new ByteArrayBuilder();
			int read = 0;
			while (read >= 0)
			{
				read = await stream.ReadByteAsync(ctsToken);
				bab.Append((byte)read);
				if (Encoding.ASCII.GetBytes(LF)[0] == (byte)read)
				{
					break;
				}
			}

			var startLine = bab.ToString(Encoding.ASCII);
			if (string.IsNullOrEmpty(startLine))
			{
				throw new FormatException($"{nameof(startLine)} cannot be null or empty.");
			}

			return startLine;
		}

		public static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken ctsToken = default)
		{
			var headers = "";
			var firstRead = true;
			var builder = new StringBuilder();
			builder.Append(headers);
			while (true)
			{
				string header = await ReadCRLFLineAsync(stream, Encoding.ASCII, ctsToken);

				if (header is null)
				{
					throw new FormatException("Malformed HTTP message: End of headers must be CRLF.");
				}

				if (header == "")
				{
					// 2 CRLF was read in row so it's the end of the headers
					break;
				}

				if (firstRead)
				{
					// https://tools.ietf.org/html/rfc7230#section-3
					// A recipient that receives whitespace between the
					// start - line and the first header field MUST either reject the message
					// as invalid or consume each whitespace-preceded line without further
					// processing of it(i.e., ignore the entire line, along with any
					// subsequent lines preceded by whitespace, until a properly formed
					// header field is received or the header section is terminated).
					if (char.IsWhiteSpace(header[0]))
					{
						throw new FormatException("Invalid HTTP message: Cannot be whitespace between the start line and the headers.");
					}
					firstRead = false;
				}

				builder.Append(header + CRLF); // CRLF is part of the headerstring
			}

			headers = builder.ToString();
			if (string.IsNullOrEmpty(headers))
			{
				headers = string.Empty;
			}

			return headers;
		}

		private static async Task<string> ReadCRLFLineAsync(Stream stream, Encoding encoding, CancellationToken ctsToken = default)
		{
			var bab = new ByteArrayBuilder();
			while (true)
			{
				int ch = await stream.ReadByteAsync(ctsToken);
				if (ch == -1)
				{
					break;
				}

				if (ch == '\r')
				{
					var ch2 = await stream.ReadByteAsync(ctsToken);
					if (ch2 == '\n')
					{
						return bab.ToString(encoding);
					}
					bab.Append(new byte[] { (byte)ch, (byte)ch2 });
					continue;
				}
				bab.Append((byte)ch);
			}

			return bab.Length > 0
				? bab.ToString(encoding)
				: null;
		}

		public static byte[] HandleGzipCompression(HttpContentHeaders contentHeaders, byte[] decodedBodyArray)
		{
			if (decodedBodyArray is null || !decodedBodyArray.Any())
			{
				return decodedBodyArray;
			}

			if (contentHeaders?.ContentEncoding != null && contentHeaders.ContentEncoding.Contains("gzip"))
			{
				using (var src = new MemoryStream(decodedBodyArray))
				using (var unzipStream = new GZipStream(src, CompressionMode.Decompress))
				{
					using (var targetStream = new MemoryStream())
					{
						unzipStream.CopyTo(targetStream);
						decodedBodyArray = targetStream.ToArray();
					}
				}
				contentHeaders.ContentEncoding.Remove("gzip");
				if (!contentHeaders.ContentEncoding.Any())
				{
					contentHeaders.Remove("Content-Encoding");
				}
			}

			return decodedBodyArray;
		}

		public static async Task<byte[]> GetContentBytesAsync(Stream stream, HttpRequestContentHeaders headerStruct, CancellationToken ctsToken = default)
		{
			if (headerStruct.RequestHeaders != null && headerStruct.RequestHeaders.Contains("Transfer-Encoding"))
			{
				// https://tools.ietf.org/html/rfc7230#section-4
				// All transfer-coding names are case-insensitive
				if ("chunked".Equals(headerStruct.RequestHeaders.TransferEncoding.Last().Value, StringComparison.OrdinalIgnoreCase))
				{
					return await GetDecodedChunkedContentBytesAsync(stream, headerStruct, ctsToken);
				}
				// https://tools.ietf.org/html/rfc7230#section-3.3.3
				// If a Transfer - Encoding header field is present in a response and
				// the chunked transfer coding is not the final encoding, the
				// message body length is determined by reading the connection until
				// it is closed by the server.  If a Transfer - Encoding header field
				// is present in a request and the chunked transfer coding is not
				// the final encoding, the message body length cannot be determined
				// reliably; the server MUST respond with the 400(Bad Request)
				// status code and then close the connection.
				return await GetBytesTillEndAsync(stream, ctsToken);
			}
			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// 5.If a valid Content - Length header field is present without
			// Transfer - Encoding, its decimal value defines the expected message
			// body length in octets.If the sender closes the connection or
			// the recipient times out before the indicated number of octets are
			// received, the recipient MUST consider the message to be
			// incomplete and close the connection.
			if (headerStruct.ContentHeaders.Contains("Content-Length"))
			{
				long? contentLength = headerStruct.ContentHeaders?.ContentLength;
				return await ReadBytesTillLengthAsync(stream, contentLength, ctsToken);
			}

			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// 6.If this is a request message and none of the above are true, then
			// the message body length is zero (no message body is present).
			// 7.  Otherwise, this is a response message without a declared message
			// body length, so the message body length is determined by the
			// number of octets received prior to the server closing the
			// connection.
			return GetDummyOrNullContentBytes(headerStruct.ContentHeaders);
		}

		public static async Task<byte[]> GetContentBytesAsync(Stream stream, HttpResponseContentHeaders headerStruct, HttpMethod requestMethod, StatusLine statusLine, CancellationToken ctsToken = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// The length of a message body is determined by one of the following
			// (in order of precedence):
			// 1.Any response to a HEAD request and any response with a 1xx
			// (Informational), 204(No Content), or 304(Not Modified) status
			// code is always terminated by the first empty line after the
			// header fields, regardless of the header fields present in the
			// message, and thus cannot contain a message body.
			if (requestMethod == HttpMethod.Head
				|| HttpStatusCodeHelper.IsInformational(statusLine.StatusCode)
				|| statusLine.StatusCode == HttpStatusCode.NoContent
				|| statusLine.StatusCode == HttpStatusCode.NotModified)
			{
				return GetDummyOrNullContentBytes(headerStruct.ContentHeaders);
			}
			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// 2.Any 2xx(Successful) response to a CONNECT request implies that
			// the connection will become a tunnel immediately after the empty
			// line that concludes the header fields.A client MUST ignore any
			// Content - Length or Transfer-Encoding header fields received in
			// such a message.
			if (requestMethod == new HttpMethod("CONNECT"))
			{
				if (HttpStatusCodeHelper.IsSuccessful(statusLine.StatusCode))
				{
					return null;
				}
			}

			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// 3.If a Transfer-Encoding header field is present and the chunked
			// transfer coding(Section 4.1) is the final encoding, the message
			// body length is determined by reading and decoding the chunked
			// data until the transfer coding indicates the data is complete.
			if (headerStruct?.ResponseHeaders != null && headerStruct.ResponseHeaders.Contains("Transfer-Encoding"))
			{
				// https://tools.ietf.org/html/rfc7230#section-4
				// All transfer-coding names are case-insensitive
				if ("chunked".Equals(headerStruct.ResponseHeaders.TransferEncoding.Last().Value, StringComparison.OrdinalIgnoreCase))
				{
					return await GetDecodedChunkedContentBytesAsync(stream, headerStruct, ctsToken);
				}
				// https://tools.ietf.org/html/rfc7230#section-3.3.3
				// If a Transfer - Encoding header field is present in a response and
				// the chunked transfer coding is not the final encoding, the
				// message body length is determined by reading the connection until
				// it is closed by the server.  If a Transfer - Encoding header field
				// is present in a request and the chunked transfer coding is not
				// the final encoding, the message body length cannot be determined
				// reliably; the server MUST respond with the 400(Bad Request)
				// status code and then close the connection.
				return await GetBytesTillEndAsync(stream, ctsToken);
			}
			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// 5.If a valid Content - Length header field is present without
			// Transfer - Encoding, its decimal value defines the expected message
			// body length in octets.If the sender closes the connection or
			// the recipient times out before the indicated number of octets are
			// received, the recipient MUST consider the message to be
			// incomplete and close the connection.
			if (headerStruct.ContentHeaders.Contains("Content-Length"))
			{
				long? contentLength = headerStruct.ContentHeaders?.ContentLength;

				return await ReadBytesTillLengthAsync(stream, contentLength, ctsToken);
			}

			// https://tools.ietf.org/html/rfc7230#section-3.3.3
			// 6.If this is a request message and none of the above are true, then
			// the message body length is zero (no message body is present).
			// 7.  Otherwise, this is a response message without a declared message
			// body length, so the message body length is determined by the
			// number of octets received prior to the server closing the
			// connection.
			return await GetBytesTillEndAsync(stream, ctsToken);
		}

		private static async Task<byte[]> GetDecodedChunkedContentBytesAsync(Stream stream, HttpRequestContentHeaders headerStruct, CancellationToken ctsToken = default)
		{
			return await GetDecodedChunkedContentBytesAsync(stream, headerStruct, null, ctsToken);
		}

		private static async Task<byte[]> GetDecodedChunkedContentBytesAsync(Stream stream, HttpResponseContentHeaders headerStruct, CancellationToken ctsToken = default)
		{
			return await GetDecodedChunkedContentBytesAsync(stream, null, headerStruct, ctsToken);
		}

		private static async Task<byte[]> GetDecodedChunkedContentBytesAsync(Stream stream, HttpRequestContentHeaders requestHeaders, HttpResponseContentHeaders responseHeaders, CancellationToken ctsToken = default)
		{
			if (responseHeaders is null)
			{
				if (requestHeaders is null)
				{
					throw new ArgumentException("Response and request headers cannot be both null.");
				}
			}
			else
			{
				if (requestHeaders != null)
				{
					throw new ArgumentException("Either response or request headers has to be null.");
				}
			}

			// https://tools.ietf.org/html/rfc7230#section-4.1.3
			// 4.1.3.Decoding Chunked
			// A process for decoding the chunked transfer coding can be represented
			// in pseudo - code as:
			// length := 0
			// read chunk - size, chunk - ext(if any), and CRLF
			// while (chunk - size > 0)
			// {
			//   read chunk-data and CRLF
			//   append chunk-data to decoded-body
			//   length:= length + chunk - size
			//   read chunk-size, chunk - ext(if any), and CRLF
			// }
			// read trailer field
			// while (trailer field is not empty) {
			//   if (trailer field is allowed to be sent in a trailer) {
			//      append trailer field to existing header fields
			//   }
			//   read trailer-field
			// }
			// Content - Length := length
			// Remove "chunked" from Transfer-Encoding
			// Remove Trailer from existing header fields
			long length = 0;
			var firstChunkLine = await ReadCRLFLineAsync(stream, Encoding.ASCII, ctsToken: ctsToken);
			ParseFistChunkLine(firstChunkLine, out long chunkSize, out _);
			// We will not do anything with the chunk extensions, because:
			// https://tools.ietf.org/html/rfc7230#section-4.1.1
			// A recipient MUST ignore unrecognized chunk extensions.

			var decodedBody = new List<byte>();
			// https://tools.ietf.org/html/rfc7230#section-4.1
			// The chunked transfer coding is complete
			// when a chunk with a chunk-size of zero is received, possibly followed
			// by a trailer, and finally terminated by an empty line.
			while (chunkSize > 0)
			{
				var chunkData = await ReadBytesTillLengthAsync(stream, chunkSize, ctsToken);
				if (await ReadCRLFLineAsync(stream, Encoding.ASCII, ctsToken) != "")
				{
					throw new FormatException("Chunk does not end with CRLF.");
				}

				decodedBody.AddRange(chunkData);

				length += chunkSize;

				firstChunkLine = await ReadCRLFLineAsync(stream, Encoding.ASCII, ctsToken: ctsToken);
				ParseFistChunkLine(firstChunkLine, out long cs, out _);
				chunkSize = cs;
			}

			// https://tools.ietf.org/html/rfc7230#section-4.1.2
			// A trailer allows the sender to include additional fields at the end
			// of a chunked message in order to supply metadata that might be
			// dynamically generated while the message body is sent
			string trailerHeaders = await ReadHeadersAsync(stream, ctsToken);
			var trailerHeaderSection = HeaderSection.CreateNew(trailerHeaders);
			RemoveInvalidTrailers(trailerHeaderSection);
			if (responseHeaders != null)
			{
				var trailerHeaderStruct = trailerHeaderSection.ToHttpResponseHeaders();
				AssertValidHeaders(trailerHeaderStruct.ResponseHeaders, trailerHeaderStruct.ContentHeaders);
				// https://tools.ietf.org/html/rfc7230#section-4.1.2
				// When a chunked message containing a non-empty trailer is received,
				// the recipient MAY process the fields(aside from those forbidden
				// above) as if they were appended to the message's header section.
				CopyHeaders(trailerHeaderStruct.ResponseHeaders, responseHeaders.ResponseHeaders);

				responseHeaders.ResponseHeaders.Remove("Transfer-Encoding");
				responseHeaders.ContentHeaders.TryAddWithoutValidation("Content-Length", length.ToString());
				responseHeaders.ResponseHeaders.Remove("Trailer");
			}
			if (requestHeaders != null)
			{
				var trailerHeaderStruct = trailerHeaderSection.ToHttpRequestHeaders();
				AssertValidHeaders(trailerHeaderStruct.RequestHeaders, trailerHeaderStruct.ContentHeaders);
				// https://tools.ietf.org/html/rfc7230#section-4.1.2
				// When a chunked message containing a non-empty trailer is received,
				// the recipient MAY process the fields(aside from those forbidden
				// above) as if they were appended to the message's header section.
				CopyHeaders(trailerHeaderStruct.RequestHeaders, requestHeaders.RequestHeaders);

				requestHeaders.RequestHeaders.Remove("Transfer-Encoding");
				requestHeaders.ContentHeaders.TryAddWithoutValidation("Content-Length", length.ToString());
				requestHeaders.RequestHeaders.Remove("Trailer");
			}

			return decodedBody.ToArray();
		}

		public static void RemoveInvalidTrailers(HeaderSection trailerHeaderSection)
		{
			// https://tools.ietf.org/html/rfc7230#section-4.1.2
			// A sender MUST NOT generate a trailer that contains a field necessary
			// for message framing (e.g., Transfer - Encoding and Content - Length),
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Transfer-Encoding");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Content-Length");
			// routing (e.g., Host)
			// request modifiers(e.g., controls and
			// https://tools.ietf.org/html/rfc7231#section-5.1
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Cache-Control");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Expect");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Host");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Max-Forwards");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Pragma");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Range");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "TE");
			// conditionals in Section 5 of[RFC7231]),
			// https://tools.ietf.org/html/rfc7231#section-5.2
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "If-Match");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "If-None-Match");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "If-Modified-Since");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "If-Unmodified-Since");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "If-Range");
			// authentication(e.g., see [RFC7235]
			// https://tools.ietf.org/html/rfc7235#section-5.3
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Authorization");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Proxy-Authenticate");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Proxy-Authorization");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "WWW-Authenticate");
			// and[RFC6265]),
			// https://tools.ietf.org/html/rfc6265
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Set-Cookie");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Cookie");
			// response control data(e.g., see Section 7.1 of[RFC7231]),
			// https://tools.ietf.org/html/rfc7231#section-7.1
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Age");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Cache-Control");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Expires");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Date");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Location");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Retry-After");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Vary");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Warning");
			// or determining how to process the payload(e.g.,
			// Content - Encoding, Content - Type, Content - Range, and Trailer).
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Content-Encoding");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Content-Type");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Content-Range");
			trailerHeaderSection.Fields.RemoveAll(x => x.Name == "Trailer");
		}

		public static void ParseFistChunkLine(string firstChunkLine, out long chunkSize, out IEnumerable<string> chunkExtensions)
		{
			// https://tools.ietf.org/html/rfc7230#section-4.1
			// chunk          = chunk-size [ chunk-ext ] CRLF
			// https://tools.ietf.org/html/rfc7230#section-4.1.1
			// chunk-ext      = *( ";" chunk-ext-name [ "=" chunk-ext-val ] )
			var parts = firstChunkLine.Split(";", StringSplitOptions.RemoveEmptyEntries);
			// https://tools.ietf.org/html/rfc7230#section-4.1
			// The chunk-size field is a string of hex digits indicating the size of
			// the chunk-data in octets.
			chunkSize = Convert.ToInt64(parts.First().Trim(), 16);
			chunkExtensions = null;
			if (parts.Length > 1)
			{
				chunkExtensions = parts.Skip(1);
			}
		}

		private static async Task<byte[]> GetBytesTillEndAsync(Stream stream, CancellationToken ctsToken)
		{
			var bab = new ByteArrayBuilder();
			while (true)
			{
				int read = await stream.ReadByteAsync(ctsToken);
				if (read == -1)
				{
					return bab.ToArray();
				}
				else
				{
					bab.Append((byte)read);
				}
			}
		}

		private static async Task<byte[]> ReadBytesTillLengthAsync(Stream stream, long? length, CancellationToken ctsToken)
		{
			try
			{
				Convert.ToInt32(length);
			}
			catch (OverflowException ex)
			{
				throw new NotSupportedException($"Content-Length too long: {length}.", ex);
			}

			var allData = new byte[(int)length];
			var num = await stream.ReadBlockAsync(allData, (int)length, ctsToken);
			if (num < (int)length)
			{
				// https://tools.ietf.org/html/rfc7230#section-3.3.3
				// If the sender closes the connection or
				// the recipient times out before the indicated number of octets are
				// received, the recipient MUST consider the message to be
				// incomplete and close the connection.
				// https://tools.ietf.org/html/rfc7230#section-3.4
				// A client that receives an incomplete response message, which can
				// occur when a connection is closed prematurely or when decoding a
				// supposedly chunked transfer coding fails, MUST record the message as
				// incomplete.Cache requirements for incomplete responses are defined
				// in Section 3 of[RFC7234].
				throw new NotSupportedException($"Incomplete message. Expected length: {length}, actual: {num}.");
			}
			return allData;
		}

		public static void AssertValidHeaders(HttpHeaders messageHeaders, HttpContentHeaders contentHeaders)
		{
			if (messageHeaders != null && messageHeaders.Contains("Transfer-Encoding"))
			{
				if (contentHeaders != null && contentHeaders.Contains("Content-Length"))
				{
					contentHeaders.Remove("Content-Length");
				}
			}
			// Any Content-Length field value greater than or equal to zero is valid.
			if (contentHeaders != null && contentHeaders.Contains("Content-Length"))
			{
				if (contentHeaders.ContentLength < 0)
				{
					throw new HttpRequestException("Content-Length MUST be larger than zero.");
				}
			}
		}

		public static byte[] GetDummyOrNullContentBytes(HttpContentHeaders contentHeaders)
		{
			if (contentHeaders.NotNullAndNotEmpty())
			{
				return new byte[] { }; // dummy empty content
			}
			return null;
		}

		public static void CopyHeaders(HttpHeaders source, HttpHeaders destination)
		{
			if (!source.NotNullAndNotEmpty())
			{
				return;
			}

			foreach (var header in source)
			{
				destination.TryAddWithoutValidation(header.Key, header.Value);
			}
		}
	}
}
