using System.Diagnostics.CodeAnalysis;
using System.Net;
using NBitcoin;

namespace WalletWasabi.Extensions;

public static class SystemNetExtensions
{
	public static string ToUriString(this EndPoint endpoint, string schema)
		=> $"{schema}://{endpoint.ToEndpointString()}";

	public static Uri ToUri(this EndPoint endpoint, string schema)
		=> new(endpoint.ToUriString(schema));

	/// <summary>
	/// Tries to get port from <paramref name="endPoint"/> which must be either <see cref="DnsEndPoint"/> or <see cref="IPEndPoint"/>.
	/// </summary>
	/// <returns><c>true</c> when port can be returned for <paramref name="endPoint"/>, <c>false</c> otherwise.</returns>
	public static bool TryGetPort(this EndPoint endPoint, [NotNullWhen(true)] out int? port)
	{
		return endPoint.TryGetHostAndPort(out _, out port);
	}

	/// <summary>
	/// Tries to get host from <paramref name="endPoint"/> which must be either <see cref="DnsEndPoint"/> or <see cref="IPEndPoint"/>.
	/// </summary>
	/// <returns><c>true</c> when host can be returned for <paramref name="endPoint"/>, <c>false</c> otherwise.</returns>
	public static bool TryGetHost(this EndPoint endPoint, [NotNullWhen(true)] out string? host)
	{
		return endPoint.TryGetHostAndPort(out host, out _);
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
