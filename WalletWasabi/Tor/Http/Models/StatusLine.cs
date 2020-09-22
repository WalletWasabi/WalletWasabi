using System;
using System.Net;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Http.Helpers;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Models
{
	public class StatusLine : StartLine
	{
		public StatusLine(HttpProtocol protocol, HttpStatusCode status) : base(protocol)
		{
			StatusCode = status;
		}

		public HttpStatusCode StatusCode { get; }

		public override string ToString()
		{
			return $"{Protocol}{SP}{(int)StatusCode}{SP}{StatusCode.ToReasonString()}{CRLF}";
		}

		public static StatusLine Parse(string statusLineString)
		{
			try
			{
				var parts = GetParts(statusLineString);
				var protocolString = parts[0];
				var codeString = parts[1];
				var reason = parts[2];
				var protocol = new HttpProtocol(protocolString);
				var code = int.Parse(codeString);
				if (!HttpStatusCodeHelper.IsValidCode(code))
				{
					throw new NotSupportedException($"Invalid HTTP status code: {code}.");
				}

				var statusCode = (HttpStatusCode)code;

				// https://tools.ietf.org/html/rfc7230#section-3.1.2
				// The reason-phrase element exists for the sole purpose of providing a
				// textual description associated with the numeric status code, mostly
				// out of deference to earlier Internet application protocols that were
				// more frequently used with interactive text clients.A client SHOULD
				// ignore the reason - phrase content.

				return new StatusLine(protocol, statusCode);
			}
			catch (Exception ex)
			{
				Logger.LogTrace(ex); // Often happens when internet connection is lost mid request.
				throw new NotSupportedException($"Invalid {nameof(StatusLine)}: {statusLineString}.", ex);
			}
		}
	}
}
