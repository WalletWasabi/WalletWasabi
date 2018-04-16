using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class Constants
	{
		public const int P2wpkhInputSizeInBytes = 41;
		public const int P2pkhInputSizeInBytes = 146;
		public const int OutputSizeInBytes = 33;
		// https://en.bitcoin.it/wiki/Bitcoin
		// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
		public const long MaximumNumberOfSatoshis = 2099999997690000;

		public static BitcoinWitPubKeyAddress GetFailedZeroLinkDosAttackAddress(Network network)
		{
			Guard.NotNull(nameof(network), network);

			if(network == Network.Main)
			{
				return new BitcoinWitPubKeyAddress("bc1qamgq88h3lylcyesjkc5jjr8rc80le78lltn7ld", Network.Main);
			}

			if (network == Network.TestNet)
			{
				return new BitcoinWitPubKeyAddress("tb1qxflvleexdwqjtcmxmwjy8fueaf3rux03lrg3qk", Network.TestNet);
			}

			// else regtest
			return new BitcoinWitPubKeyAddress("bcrt1q2gqv6rnarkmprd2wgtmkgh5wte4ungapgazess", Network.RegTest);
		}
	}
}
