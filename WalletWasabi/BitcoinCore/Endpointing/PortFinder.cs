using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Endpointing
{
	public static class PortFinder
	{
		public static int[] GetRandomPorts(int num)
		{
			int[] portArray = new int[num];
			var i = 0;
			while (i < portArray.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port += 10000;
				if (portArray.Any(p => p == port))
				{
					continue;
				}

				try
				{
					var listener = new TcpListener(IPAddress.Loopback, (int)port);
					listener.Start();
					listener.Stop();
					portArray[i] = (int)port;
					i++;
				}
				catch (SocketException ex)
				{
					Logger.LogTrace(ex);
				}
			}

			return portArray;
		}
	}
}
