using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain
{
	public class SmartHeader
	{
		public uint256 BlockHash { get; }
		public uint256 PrevHash { get; }
		public uint Height { get; }
		public DateTimeOffset BlockTime { get; }
		public GolombRiceFilter Filter { get; }

		public SmartHeader(uint256 blockHash, uint256 prevHash, uint height, DateTimeOffset blockTime, GolombRiceFilter filter) : this(blockHash, height, blockTime, filter)
		{
			if (blockHash == prevHash)
			{
				throw new InvalidOperationException($"{nameof(blockHash)} cannot be equal to {nameof(prevHash)}. Value: {blockHash}.");
			}

			PrevHash = Guard.NotNull(nameof(prevHash), prevHash);
		}

		private SmartHeader(uint256 blockHash, uint height, DateTimeOffset blockTime, GolombRiceFilter filter)
		{
			BlockHash = Guard.NotNull(nameof(blockHash), blockHash);
			Height = height;
			BlockTime = blockTime;
			Filter = filter;
		}

		#region SpecialHeaders

		private static SmartHeader StartingHeaderMain { get; } = new SmartHeader(
			new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893"),
			481824,
			DateTimeOffset.FromUnixTimeSeconds(1503539857),
			new GolombRiceFilter(Encoders.Hex.DecodeData("02832810ec08a0"), 20, 1 << 20));

		private static SmartHeader StartingHeaderTestNet { get; } = new SmartHeader(
			new uint256("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a"),
			828575,
			DateTimeOffset.FromUnixTimeSeconds(1463079943),
			new GolombRiceFilter(Encoders.Hex.DecodeData("017821b8"), 20, 1 << 20));

		private static SmartHeader StartingHeaderRegTest { get; } = new SmartHeader(
			Network.RegTest.GenesisHash,
			0,
			Network.RegTest.GetGenesis().Header.BlockTime,
			null);

		/// <summary>
		/// Where the first possible bech32 transaction ever can be found.
		/// </summary>
		public static SmartHeader GetStartingHeader(Network network)
		{
			return network.NetworkType switch
			{
				NetworkType.Mainnet => StartingHeaderMain,
				NetworkType.Testnet => StartingHeaderTestNet,
				NetworkType.Regtest => StartingHeaderRegTest,
				_ => throw new NotSupportedNetworkException(network)
			};
		}

		#endregion SpecialHeaders
	}
}
