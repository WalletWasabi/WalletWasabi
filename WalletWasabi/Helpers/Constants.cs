﻿using NBitcoin;
using NBitcoin.Protocol;
using System;
using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.Helpers
{
	public static class Constants
	{
		public static readonly Version ClientVersion = new Version(1, 1, 0);
		public const string BackendMajorVersion = "3";
		public static readonly VersionsResponse VersionsResponse = new VersionsResponse { ClientVersion = ClientVersion.ToString(), BackenMajordVersion = BackendMajorVersion };

		public const uint ProtocolVersion_WITNESS_VERSION = 70012;

		public const int MaxPasswordLength = 100;

		public static readonly NodeRequirement NodeRequirements = new NodeRequirement
		{
			RequiredServices = NodeServices.NODE_WITNESS,
			MinVersion = ProtocolVersion_WITNESS_VERSION,
			MinProtocolCapabilities = new ProtocolCapabilities { SupportGetBlock = true, SupportWitness = true, SupportMempoolQuery = true }
		};

		public const int P2wpkhInputSizeInBytes = 41;
		public const int P2pkhInputSizeInBytes = 145;
		public const int OutputSizeInBytes = 33;

		// https://en.bitcoin.it/wiki/Bitcoin
		// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
		public const long MaximumNumberOfSatoshis = 2099999997690000;

		private static readonly BitcoinWitPubKeyAddress MainNetCoordinatorAddress = new BitcoinWitPubKeyAddress("bc1qs604c7jv6amk4cxqlnvuxv26hv3e48cds4m0ew", Network.Main);
		private static readonly BitcoinWitPubKeyAddress TestNetCoordinatorAddress = new BitcoinWitPubKeyAddress("tb1qecaheev3hjzs9a3w9x33wr8n0ptu7txp359exs", Network.TestNet);
		private static readonly BitcoinWitPubKeyAddress RegTestCoordinatorAddress = new BitcoinWitPubKeyAddress("bcrt1qangxrwyej05x9mnztkakk29s4yfdv4n586gs8l", Network.RegTest);

		public static BitcoinWitPubKeyAddress GetCoordinatorAddress(Network network)
		{
			Guard.NotNull(nameof(network), network);

			if (network == Network.Main)
			{
				return MainNetCoordinatorAddress;
			}

			if (network == Network.TestNet)
			{
				return TestNetCoordinatorAddress;
			}

			// else regtest
			return RegTestCoordinatorAddress;
		}

		public const string ChangeOfSpecialLabelStart = "change of (";
		public const string ChangeOfSpecialLabelEnd = ")";
	}
}
