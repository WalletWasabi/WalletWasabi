using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace System.Net
{
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
				throw new FormatException($"Invalid endpoint: {me.ToString()}");
			}

			if (port == 0)
			{
				port = defaultPort;
			}

			var endPointString = $"{host}:{port}";

			return endPointString;
		}

		/// <param name="defaultPort">If invalid and it's needed to use, then this function returns false.</param>
		public static bool TryParse(string endPointString, int defaultPort, out EndPoint endPoint)
		{
			endPoint = null;

			try
			{
				if (string.IsNullOrWhiteSpace(endPointString))
				{
					return false;
				}

				endPointString = Guard.Correct(endPointString);
				endPointString = endPointString.TrimEnd(':', '/');
				endPointString = endPointString.TrimStart("bitcoin-p2p://", StringComparison.OrdinalIgnoreCase);
				endPointString = endPointString.TrimStart("tcp://", StringComparison.OrdinalIgnoreCase);

				var parts = endPointString.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().TrimEnd('/').TrimEnd()).ToArray();

				if (parts.Length == 0)
				{
					return false;
				}

				ushort p;
				int port;

				if (parts.Length == 1)
				{
					if (IsValidPort(defaultPort.ToString(), out p))
					{
						port = p;
					}
					else
					{
						return false;
					}
				}
				else if (parts.Length == 2)
				{
					var portString = parts[1];

					if (IsValidPort(portString, out p))
					{
						port = p;
					}
					else
					{
						return false;
					}
				}
				else
				{
					return false;
				}

				var host = parts[0];

				if (host == "localhost")
				{
					host = IPAddress.Loopback.ToString();
				}

				if (IPAddress.TryParse(host, out IPAddress addr))
				{
					endPoint = new IPEndPoint(addr, port);
				}
				else
				{
					endPoint = new DnsEndPoint(host, port);
				}

				return true;
			}
			catch
			{
				return false;
			}
		}

		// Checks a port is a number within the valid port range (0 - 65535).
		private static bool IsValidPort(string port, out ushort p)
		{
			return ushort.TryParse(port, out p);
		}
	}
}
