using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;

namespace WalletWasabi.TorSocks5
{
	public class TorProcessManager
	{
		public IPEndPoint TorSocks5EndPoint { get; }

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		public TorProcessManager(IPEndPoint torSocks5EndPoint = null)
		{
			TorSocks5EndPoint = torSocks5EndPoint ?? new IPEndPoint(IPAddress.Loopback, 9050);
		}

		public async Task StartAsync()
		{
			// 1. Is it already running?
			// 2. Can I simply run it from output directory?
			// 3. Can I copy and unzip it from assets?
			// 4. Throw exception.
			if (await IsTorRunningAsync())
			{
				return;
			}

			throw new NotImplementedException();
		}

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		public static async Task<bool> IsTorRunningAsync(IPEndPoint torSocks5EndPoint = null)
		{
			using (var client = new TorSocks5Client(torSocks5EndPoint))
			{
				try
				{
					await client.ConnectAsync();
					await client.HandshakeAsync();
				}
				catch (ConnectionException)
				{
					return false;
				}
				return true;
			}
		}
	}
}
