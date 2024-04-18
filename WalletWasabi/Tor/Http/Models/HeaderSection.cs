using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Models;

public class HeaderSection
{
	private const string ContentLengthHeaderName = "Content-Length";

	public List<HeaderField> Fields { get; private set; } = new();

	public override string ToString()
	{
		StringBuilder sb = new();

		foreach (HeaderField field in Fields)
		{
			sb.Append(field.ToString(endWithCRLF: true));
		}

		return sb.ToString();
	}

	public static async Task<HeaderSection> CreateNewAsync(string headersString)
	{
		headersString = HeaderField.CorrectObsFolding(headersString);

		var hs = new HeaderSection();
		if (headersString.EndsWith(CRLF + CRLF))
		{
			headersString = headersString.TrimEnd(CRLF, StringComparison.Ordinal);
		}

		using var reader = new StringReader(headersString);
		while (true)
		{
			var field = reader.ReadLine(strictCRLF: true);
			if (field is null)
			{
				break;
			}
			hs.Fields.Add(await HeaderField.CreateNewAsync(field).ConfigureAwait(false));
		}

		ValidateAndCorrectHeaders(hs);

		return hs;
	}

	private static void ValidateAndCorrectHeaders(HeaderSection hs)
	{
		// https://tools.ietf.org/html/rfc7230#section-5.4
		// Since the Host field - value is critical information for handling a
		// request, a user agent SHOULD generate Host as the first header field
		// following the request - line.
		HeaderField? hostToCorrect = null;
		foreach (var f in hs.Fields)
		{
			// if we find host
			if (f.IsNameEqual("Host"))
			{
				// if host is not first
				if (!hs.Fields.First().IsNameEqual("Host"))
				{
					// then correct host
					hostToCorrect = f;
					break;
				}
			}
		}

		if (hostToCorrect is { })
		{
			hs.Fields.Remove(hostToCorrect);
			hs.Fields.Insert(0, hostToCorrect);
		}

		// https://tools.ietf.org/html/rfc7230#section-3.3.2
		// If a message is received that has multiple Content-Length header
		// fields with field-values consisting of the same decimal value, or a
		// single Content-Length header field with a field value containing a
		// list of identical decimal values(e.g., "Content-Length: 42, 42"),
		// indicating that duplicate Content-Length header fields have been
		// generated or combined by an upstream message processor, then the
		// recipient MUST either reject the message as invalid or replace the
		// duplicated field - values with a single valid Content-Length field
		// containing that decimal value prior to determining the message body
		// length or forwarding the message.

		var allParts = new HashSet<string>();
		foreach (var field in hs.Fields)
		{
			if (field.IsNameEqual(ContentLengthHeaderName))
			{
				var parts = field.Value.Trim().Split(',');
				foreach (var part in parts)
				{
					allParts.Add(part.Trim());
				}
			}
		}
		if (allParts.Count > 0) // then has Content-Length field
		{
			if (allParts.Count > 1)
			{
				throw new InvalidDataException("Invalid Content-Length.");
			}
			hs.Fields.RemoveAll(x => x.IsNameEqual(ContentLengthHeaderName));
			hs.Fields.Add(new HeaderField(ContentLengthHeaderName, allParts.First()));
		}
	}

	public HttpRequestContentHeaders ToHttpRequestHeaders()
	{
		using var message = new HttpRequestMessage
		{
			Content = new ByteArrayContent(Array.Empty<byte>())
		};
		message.Content.Headers.ContentLength = null;
		foreach (var field in Fields)
		{
			if (field.Name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
			{
				message.Content.Headers.TryAddWithoutValidation(field.Name, field.Value);
			}
			else
			{
				message.Headers.TryAddWithoutValidation(field.Name, field.Value);
			}
		}

		return new HttpRequestContentHeaders(message.Headers, message.Content.Headers);
	}

	public HttpResponseContentHeaders ToHttpResponseHeaders()
	{
		using var message = new HttpResponseMessage
		{
			Content = new ByteArrayContent(Array.Empty<byte>())
		};
		message.Content.Headers.ContentLength = null;
		foreach (var field in Fields)
		{
			if (field.Name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
			{
				message.Content.Headers.TryAddWithoutValidation(field.Name, field.Value);
			}
			else
			{
				message.Headers.TryAddWithoutValidation(field.Name, field.Value);
			}
		}

		return new HttpResponseContentHeaders(message.Headers, message.Content.Headers);
	}

	public static HeaderSection CreateNew(HttpHeaders headers)
	{
		var hs = new HeaderSection();
		foreach (var header in headers)
		{
			hs.Fields.Add(new HeaderField(header.Key, string.Join(",", header.Value)));
		}

		// -- Start [SECTION] Crazy VS2017/.NET Core 1.1 bug ---
		// The following if branch is needed as is to avoid the craziest VS2017/.NET Core 1.1 bug I have ever seen!
		// If this section is not added the Content-Length header will not be set unless...
		// - I put a break point at the start of the function
		// - And I explicitly expand the "headers" variable
		if (headers is HttpContentHeaders contentHeaders && contentHeaders.ContentLength is { } contentLength)
		{
			if (hs.Fields.All(x => !x.IsNameEqual(ContentLengthHeaderName)))
			{
				hs.Fields.Add(new HeaderField(ContentLengthHeaderName, contentLength.ToString()));
			}
		}

		// -- End [SECTION] Crazy VS2017/.NET Core 1.1 bug ---

		return hs;
	}
}
