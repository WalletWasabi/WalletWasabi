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

        /// <param name="defaultPort">If set to -1 and it's needed to use, then this function returns false. Use 0 if you want to just discard the port.</param>
        public static bool TryParse(string endPointString, int defaultPort, out EndPoint endPoint)
        {
            endPoint = null;

            if (string.IsNullOrWhiteSpace(endPointString))
            {
                return false;
            }

            endPointString = Guard.Correct(endPointString);
            endPointString = endPointString.TrimEnd(':', '/');
            var parts = endPointString.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().TrimEnd('/').TrimEnd()).ToArray();

            int port;
            if (parts.Length == 0)
            {
                return false;
            }
            else if (parts.Length == 1)
            {
                if (defaultPort == -1)
                {
                    return false;
                }
                else
                {
                    port = defaultPort;
                }
            }
            else if (parts.Length == 2)
            {
                var portString = parts[1];
                if (int.TryParse(portString, out int p))
                {
                    port = p;
                }
                else
                {
                    if (defaultPort == -1)
                    {
                        return false;
                    }

                    port = defaultPort;
                }
            }
            else
            {
                return false;
            }

            string host = parts[0];
            if (host == "localhost")
            {
                endPoint = new IPEndPoint(IPAddress.Loopback, port);
            }
            else if (IPAddress.TryParse(host, out IPAddress addr))
            {
                endPoint = new IPEndPoint(addr, port);
            }
            else
            {
                try
                {
                    endPoint = new DnsEndPoint(host, port);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
    }
}
