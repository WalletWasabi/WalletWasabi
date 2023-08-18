using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Affiliation;
using WalletWasabi.Tor.Http.Helpers;
using WalletWasabi.Tor.Http.Models;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Tor.Http.Extensions;

public static class HttpResponseMessageExtensions
{
	public static async Task<HttpResponseMessage> CreateNewAsync(Stream responseStream, HttpMethod requestMethod, CancellationToken cancellationToken)
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

		string startLine = await HttpMessageHelper.ReadStartLineAsync(responseStream, cancellationToken).ConfigureAwait(false);

		StatusLine statusLine = StatusLine.Parse(startLine);
		HttpResponseMessage response = new(statusLine.StatusCode);

		string headers = await HttpMessageHelper.ReadHeadersAsync(responseStream, cancellationToken).ConfigureAwait(false);

		HeaderSection headerSection = await HeaderSection.CreateNewAsync(headers).ConfigureAwait(false);
		HttpResponseContentHeaders headerStruct = headerSection.ToHttpResponseHeaders();

		HttpMessageHelper.AssertValidHeaders(headerStruct.ResponseHeaders, headerStruct.ContentHeaders);
		byte[]? contentBytes = await HttpMessageHelper.GetContentBytesAsync(responseStream, headerStruct, requestMethod, statusLine, cancellationToken).ConfigureAwait(false);

		HttpMessageHelper.CopyHeaders(headerStruct.ResponseHeaders, response.Headers);

		if (contentBytes is not null)
		{
			contentBytes = HttpMessageHelper.DecompressGzipContentIfRequired(headerStruct.ContentHeaders, contentBytes);
			response.Content = new ByteArrayContent(contentBytes);
			HttpMessageHelper.CopyHeaders(headerStruct.ContentHeaders, response.Content.Headers);
		}
		else
		{
			response.Content = null;
		}

		return response;
	}

	public static async Task ThrowUnwrapExceptionFromContentAsync(this HttpResponseMessage me, CancellationToken cancellationToken)
	{
		try
		{
			await me.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e) when (e.InnerException is { } innerException)
		{
			throw innerException;
		}
	}

	public static async Task ThrowRequestExceptionFromContentAsync(this HttpResponseMessage me, CancellationToken cancellationToken)
	{
		var errorMessage = "";

		if (me.Content is not null)
		{
			var contentString = await me.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var error = JsonConvert.DeserializeObject<Error>(
				contentString,
				new JsonSerializerSettings()
				{
					Converters = JsonSerializationOptions.Default.Settings.Converters,
					Error = (_, e) => e.ErrorContext.Handled = true // Try to deserialize an Error object
				});
			var innerException = error switch
			{
				{ Type: ProtocolConstants.ProtocolViolationType } => Enum.TryParse<WabiSabiProtocolErrorCode>(error.ErrorCode, out var code)
					? new WabiSabiProtocolException(code, error.Description, exceptionData: error.ExceptionData)
					: new NotSupportedException($"Received WabiSabi protocol exception with unknown '{error.ErrorCode}' error code.\n\tDescription: '{error.Description}'."),
				{ Type: AffiliationConstants.RequestSecrecyViolationType } => new AffiliationException(error.Description),
				{ Type: "unknown" } => new Exception(error.Description),
				_ => null
			};

			if (innerException is not null)
			{
				throw new HttpRequestException("Remote coordinator responded with an error.", innerException, me.StatusCode);
			}

			// Remove " from beginning and end to ensure backwards compatibility and it's kind of trash, too.
			if (contentString.Count(f => f == '"') <= 2)
			{
				contentString = contentString.Trim('"');
			}

			if (!string.IsNullOrWhiteSpace(contentString))
			{
				errorMessage = $"\n{contentString}";
			}
		}

		throw new HttpRequestException($"{me.StatusCode.ToReasonString()}{errorMessage}");
	}
}
