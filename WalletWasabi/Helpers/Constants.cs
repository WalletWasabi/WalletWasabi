using NBitcoin;
using NBitcoin.Protocol;
using System;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Helpers
{
	public static class Constants
	{
		public static readonly Version ClientVersion = new Version(1, 1, 10, 0);
		public const string BackendMajorVersion = "3";
		public static readonly Version HwiVersion = new Version("1.0.3");
		public static readonly Version BitcoinCoreVersion = new Version("0.19.0.1");

		/// <summary>
		/// By changing this, we can force to start over the transactions file, so old incorrect transactions would be cleared.
		/// It is also important to force the KeyManagers to be reindexed when this is changed by renaming the BlockState Height related property.
		/// </summary>
		public const string ConfirmedTransactionsVersion = "2";

		public const uint ProtocolVersionWitnessVersion = 70012;

		public static readonly NodeRequirement NodeRequirements = new NodeRequirement
		{
			RequiredServices = NodeServices.NODE_WITNESS,
			MinVersion = ProtocolVersionWitnessVersion,
			MinProtocolCapabilities = new ProtocolCapabilities { SupportGetBlock = true, SupportWitness = true, SupportMempoolQuery = true }
		};

		public static readonly NodeRequirement LocalNodeRequirements = new NodeRequirement
		{
			RequiredServices = NodeServices.NODE_WITNESS,
			MinVersion = ProtocolVersionWitnessVersion,
			MinProtocolCapabilities = new ProtocolCapabilities { SupportGetBlock = true, SupportWitness = true }
		};

		public static readonly NodeRequirement LocalBackendNodeRequirements = new NodeRequirement
		{
			RequiredServices = NodeServices.NODE_WITNESS,
			MinVersion = ProtocolVersionWitnessVersion,
			MinProtocolCapabilities = new ProtocolCapabilities
			{
				SupportGetBlock = true,
				SupportWitness = true,
				SupportMempoolQuery = true,
				SupportSendHeaders = true,
				SupportPingPong = true,
				PeerTooOld = true
			}
		};

		public const int P2wpkhInputSizeInBytes = 41;
		public const int P2pkhInputSizeInBytes = 145;
		public const int OutputSizeInBytes = 33;

		// https://en.bitcoin.it/wiki/Bitcoin
		// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
		public const long MaximumNumberOfSatoshis = 2099999997690000;

		private static readonly BitcoinWitPubKeyAddress MainNetCoordinatorAddress = new BitcoinWitPubKeyAddress("bc1qa24tsgchvuxsaccp8vrnkfd85hrcpafg20kmjw", Network.Main);
		private static readonly BitcoinWitPubKeyAddress TestNetCoordinatorAddress = new BitcoinWitPubKeyAddress("tb1qecaheev3hjzs9a3w9x33wr8n0ptu7txp359exs", Network.TestNet);
		private static readonly BitcoinWitPubKeyAddress RegTestCoordinatorAddress = new BitcoinWitPubKeyAddress("bcrt1qangxrwyej05x9mnztkakk29s4yfdv4n586gs8l", Network.RegTest);

		public static BitcoinWitPubKeyAddress GetCoordinatorAddress(Network network)
		{
			Guard.NotNull(nameof(network), network);

			if (network == Network.Main)
			{
				return MainNetCoordinatorAddress;
			}
			else if (network == Network.TestNet)
			{
				return TestNetCoordinatorAddress;
			}
			else if (network == Network.RegTest)
			{
				return RegTestCoordinatorAddress;
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}

		public const int TwentyMinutesConfirmationTarget = 2;
		public const int OneDayConfirmationTarget = 144;
		public const int SevenDaysConfirmationTarget = 1008;

		public const int BigFileReadWriteBufferSize = 1 * 1024 * 1024;

		public const int DefaultTorSocksPort = 9050;
		public const int DefaultTorBrowserSocksPort = 9150;
		public const int DefaultTorControlPort = 9051;
		public const int DefaultTorBrowserControlPort = 9151;

		public const int DefaultMainNetBitcoinP2pPort = 8333;
		public const int DefaultTestNetBitcoinP2pPort = 18333;
		public const int DefaultRegTestBitcoinP2pPort = 18444;

		public const int DefaultMainNetBitcoinCoreRpcPort = 8332;
		public const int DefaultTestNetBitcoinCoreRpcPort = 18332;
		public const int DefaultRegTestBitcoinCoreRpcPort = 18443;

		public const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

		public const double TransactionRBFSignalRate = 0.02; // 2% RBF transactions

		public const decimal DefaultDustThreshold = 0.00005m;

		public const long MaxSatoshisSupply = 2_100_000_000_000_000L;
	}
}
