using System;
using System.Collections.Generic;
using System.Text;

namespace System.Net
{
	public static class SystemNetExtensions
	{
		public static int? GetPortOrDefault(this EndPoint me)
		{
			int port;
			if (me is DnsEndPoint dnsEndPoint)
			{
				port = dnsEndPoint.Port;
			}
			else if (me is IPEndPoint ipEndPoint)
			{
				port = ipEndPoint.Port;
			}
			else
			{
				return null;
			}

			return port;
		}

		public static string GetHostOrDefault(this EndPoint me)
		{
			string host;
			if (me is DnsEndPoint dnsEndPoint)
			{
				host = dnsEndPoint.Host;
			}
			else if (me is IPEndPoint ipEndPoint)
			{
				host = ipEndPoint.Address.ToString();
			}
			else
			{
				return null;
			}

			return host;
		}
	}
}
