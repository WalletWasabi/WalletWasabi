using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness
{
	public static class IWasabiRandomExtensions
	{
		public static byte[] GetBytes(this IWasabiRandom me, int length)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);
			var buffer = new byte[length];
			me.GetBytes(buffer);
			return buffer;
		}
	}
}
