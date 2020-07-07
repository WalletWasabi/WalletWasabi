using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.Randomness
{
	public interface IWasabiRandom : IRandom
	{
		public byte[] GetBytes(int length)
		{
			var buffer = new byte[length];
			GetBytes(buffer);
			return buffer;
		}
	}
}
