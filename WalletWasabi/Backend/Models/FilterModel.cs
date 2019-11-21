using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Models
{
	public class FilterModel
	{
		public SmartHeader Header { get; }

		public GolombRiceFilter Filter { get; }

		public FilterModel(SmartHeader header, GolombRiceFilter filter)
		{
			Header = header;
			Filter = Guard.NotNull(nameof(filter), filter);
		}

		// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
		// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
		// is constructed.This ensures the key is deterministic while still varying from block to block.
		public byte[] FilterKey => Header.BlockHash.ToBytes().Take(16).ToArray();

		public string ToLine()
		{
			var builder = new StringBuilder();
			builder.Append(Header.Height);
			builder.Append(":");
			builder.Append(Header.BlockHash);
			builder.Append(":");
			builder.Append(Filter);
			builder.Append(":");
			builder.Append(Header.PrevHash);
			builder.Append(":");
			builder.Append(Header.BlockTime.ToUnixTimeSeconds());

			return builder.ToString();
		}

		public static FilterModel FromLine(string line)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
			string[] parts = line.Split(':');

			if (parts.Length < 5)
			{
				throw new ArgumentException(nameof(line), line);
			}

			var blockHeight = uint.Parse(parts[0]);
			var blockHash = uint256.Parse(parts[1]);
			var filterData = Encoders.Hex.DecodeData(parts[2]);
			GolombRiceFilter filter = new GolombRiceFilter(filterData, 20, 1 << 20);
			var prevBlockHash = uint256.Parse(parts[3]);
			var blockTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[4]));

			return new FilterModel(new SmartHeader(blockHash, prevBlockHash, blockHeight, blockTime), filter);
		}
	}
}
