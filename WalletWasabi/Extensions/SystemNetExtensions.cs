using System.Diagnostics.CodeAnalysis;

namespace System.Net
{
	public static class SystemNetExtensions
	{
		/// <summary>
		/// Tries to get port from <paramref name="endPoint"/> which must be either <see cref="DnsEndPoint"/> or <see cref="IPEndPoint"/>.
		/// </summary>
		/// <returns><c>true</c> when port can be returned for <paramref name="endPoint"/>, <c>false</c> otherwise.</returns>
		public static bool TryGetPort(this EndPoint endPoint, [NotNullWhen(true)] out int? port)
		{
			if (endPoint is DnsEndPoint dnsEndPoint)
			{
				port = dnsEndPoint.Port;
				return true;
			}
			else if (endPoint is IPEndPoint ipEndPoint)
			{
				port = ipEndPoint.Port;
				return true;
			}
			else
			{
				port = null;
				return false;
			}
		}

		/// <summary>
		/// Tries to get host from <paramref name="endPoint"/> which must be either <see cref="DnsEndPoint"/> or <see cref="IPEndPoint"/>.
		/// </summary>
		/// <returns><c>true</c> when host can be returned for <paramref name="endPoint"/>, <c>false</c> otherwise.</returns>
		public static bool TryGetHost(this EndPoint endPoint, [NotNullWhen(true)] out string? host)
		{
			if (endPoint is DnsEndPoint dnsEndPoint)
			{
				host = dnsEndPoint.Host;
				return true;
			}
			else if (endPoint is IPEndPoint ipEndPoint)
			{
				host = ipEndPoint.Address.ToString();
				return true;
			}
			else
			{
				host = null;
				return false;
			}
		}

		/// <summary>
		/// Tries to get host and port from <paramref name="endPoint"/> which must be either <see cref="DnsEndPoint"/> or <see cref="IPEndPoint"/>.
		/// </summary>
		/// <returns><c>true</c> when host and port can be returned for <paramref name="endPoint"/>, <c>false</c> otherwise.</returns>
		public static bool TryGetHostAndPort(this EndPoint endPoint, [NotNullWhen(true)] out string? host, [NotNullWhen(true)] out int? port)
		{
			if (endPoint is DnsEndPoint dnsEndPoint)
			{
				host = dnsEndPoint.Host;
				port = dnsEndPoint.Port;
				return true;
			}
			else if (endPoint is IPEndPoint ipEndPoint)
			{
				host = ipEndPoint.Address.ToString();
				port = ipEndPoint.Port;
				return true;
			}
			else
			{
				host = null;
				port = null;
				return false;
			}
		}
	}
}
