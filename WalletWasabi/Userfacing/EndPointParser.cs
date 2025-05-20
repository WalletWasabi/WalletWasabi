using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Userfacing;

public static class EndPointParser
{
	public static string ToString(this EndPoint me, int defaultPort)
	{
		string host;
		int port;
		if (me is DnsEndPoint dnsEndPoint)
		{
			host = dnsEndPoint.Host;
			port = dnsEndPoint.Port;
		}
		else if (me is IPEndPoint ipEndPoint)
		{
			host = ipEndPoint.Address.ToString();
			port = ipEndPoint.Port;
		}
		else
		{
			throw new FormatException($"Invalid endpoint: {me}");
		}

		if (port == 0)
		{
			port = defaultPort;
		}

		var endPointString = $"{host}:{port}";

		return endPointString;
	}

	public static bool TryParse(string endPointString, [NotNullWhen(true)] out EndPoint? endPoint)
	{
		try
		{
			endPoint = Parse(endPointString);
			return true;
		}
		catch (Exception)
		{
			endPoint = null;
			return false;
		}
	}

	public static EndPoint Parse(string endpointString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpointString, nameof(endpointString));
		if (IPEndPoint.TryParse(endpointString, out var ipEndPoint))
		{
			return ipEndPoint;
		}

		if (TyrParseDnsEndPoint(endpointString, out var dnsEndPoint))
		{
			return dnsEndPoint;
		}
		throw new FormatException("The string doesn't represent a valid endpoint");
	}

	public static bool TyrParseDnsEndPoint(string endPointString, [NotNullWhen(true)] out DnsEndPoint? endPoint)
	{
		try
		{
			endPoint = ParseDnsEndPoint(endPointString);
			return true;
		}
		catch (Exception)
		{
			endPoint = null;
			return false;
		}
	}

	public static DnsEndPoint ParseDnsEndPoint(string endpointString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpointString, nameof(endpointString));

		var parts = endpointString.Split(':');
		if (parts.Length != 2)
		{
			throw new FormatException("Endpoint must be in the format 'host:port' or 'ip:port'");
		}

		var hostOrIp = parts[0].Trim();

		if (!uint.TryParse(parts[1].Trim(), out var port))
		{
			throw new FormatException("Port must be a valid 16 bits integer");
		}

		return new DnsEndPoint(hostOrIp, (int) port);
	}
}
