using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Helpers
{
	public static class Constants
	{
		public const int P2wpkhInputSizeInBytes = 41;
		public const int P2pkhInputSizeInBytes = 146;
		public const int OutputSizeInBytes = 33;
		// https://en.bitcoin.it/wiki/Bitcoin
		// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
		public const long MaximumNumberOfSatoshis = 2099999997690000;
	}
}
