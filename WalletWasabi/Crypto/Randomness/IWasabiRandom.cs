using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness
{
	public interface IWasabiRandom : IRandom
	{
		public byte[] GetBytes(int length)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);
			var buffer = new byte[length];
			GetBytes(buffer);
			return buffer;
		}
	}
}
