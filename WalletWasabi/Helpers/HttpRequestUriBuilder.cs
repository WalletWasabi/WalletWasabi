using WalletWasabi.Http.Models;

namespace System.Net.Http
{
	public class HttpRequestUriBuilder : UriBuilder
	{
		public HttpRequestUriBuilder(UriScheme uriScheme, string host, string path = "")
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.3
			// The scheme and host are case-insensitive and normally provided in lowercase;
			// all other components are compared in a case-sensitive manner.

			if (host is null)
			{
				throw new FormatException("Host identifier cannot be null.");
			}

			var h = host.Trim().TrimEnd('/').TrimStart(uriScheme.ToString() + "://", StringComparison.OrdinalIgnoreCase);
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			if (h.Length == 0)
			{
				throw new FormatException("Host identifier is empty.");
			}

			Host = h;

			Scheme = uriScheme.ToString().ToLowerInvariant();
			if (uriScheme == UriScheme.http)
			{
				// https://tools.ietf.org/html/rfc7230#section-2.7.1
				// [http] If the port subcomponent is empty or not given, TCP port 80(the reserved port
				// for WWW services) is the default.
				Port = 80;
			}
			else if (uriScheme == UriScheme.https)
			{
				// https://tools.ietf.org/html/rfc7230#section-2.7.2
				// [https] TCP port 443 is the default if the port subcomponent is empty or not given
				Port = 443;
			}

			// Because we want to tolerate http:// and https:// in the host we also want to make sure it does not contradict the scheme
			foreach (UriScheme scheme in Enum.GetValues(typeof(UriScheme)))
			{
				// if host starts with http:// or https:// then check
				if (host.StartsWith(scheme.ToString() + "://", StringComparison.OrdinalIgnoreCase))
				{
					// if the currently iterated scheme does not equal the provided scheme
					if (scheme != uriScheme)
					{
						throw new FormatException($"{nameof(uriScheme)} not consistent with host identifier.");
					}
				}
			}

			Path = path;
		}

		public Uri BuildUri(string path)
		{
			Path = path;
			return Uri;
		}
	}
}
