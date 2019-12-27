using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Configuration.Whitening
{
	public abstract class WhiteEntry
	{
		public string Permissions { get; private set; } = string.Empty;
		public EndPoint EndPoint { get; private set; } = null;

		public static bool TryParse<T>(string value, Network network, out T whiteEntry) where T : WhiteEntry, new()
		{
			whiteEntry = null;
			// https://github.com/bitcoin/bitcoin/pull/16248
			var parts = value?.Split('@');
			if (parts is { })
			{
				if (EndPointParser.TryParse(parts.LastOrDefault(), network.DefaultPort, out EndPoint endPoint))
				{
					whiteEntry = new T();
					whiteEntry.EndPoint = endPoint;
					if (parts.Length > 1)
					{
						whiteEntry.Permissions = parts.First();
					}

					return true;
				}
			}

			return false;
		}
	}
}
