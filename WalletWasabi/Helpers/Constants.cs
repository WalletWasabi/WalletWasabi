using NBitcoin;

namespace WalletWasabi.Helpers
{
	public static class Constants
	{
		public const uint ProtocolVersion_WITNESS_VERSION = 70012;

		public const int P2wpkhInputSizeInBytes = 41;
		public const int P2pkhInputSizeInBytes = 146;
		public const int OutputSizeInBytes = 33;

		// https://en.bitcoin.it/wiki/Bitcoin
		// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
		public const long MaximumNumberOfSatoshis = 2099999997690000;

		public static BitcoinWitPubKeyAddress GetCoordinatorAddress(Network network)
		{
			Guard.NotNull(nameof(network), network);

			if (network == Network.Main)
			{
				return new BitcoinWitPubKeyAddress("bc1qs604c7jv6amk4cxqlnvuxv26hv3e48cds4m0ew", Network.Main);
			}

			if (network == Network.TestNet)
			{
				return new BitcoinWitPubKeyAddress("tb1qecaheev3hjzs9a3w9x33wr8n0ptu7txp359exs", Network.TestNet);
			}

			// else regtest
			return new BitcoinWitPubKeyAddress("bcrt1qangxrwyej05x9mnztkakk29s4yfdv4n586gs8l", Network.RegTest);
		}
	}
}
