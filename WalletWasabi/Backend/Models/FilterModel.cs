using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Models
{
	public class FilterModel
	{
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height BlockHeight { get; }

		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 BlockHash { get; }

		[JsonConverter(typeof(GolombRiceFilterJsonConverter))]
		public GolombRiceFilter Filter { get; }

		public FilterModel(Height blockHeight, uint256 blockHash, GolombRiceFilter filter)
		{
			BlockHeight = blockHeight;
			BlockHash = Guard.NotNull(nameof(blockHash), blockHash);
			Filter = Guard.NotNull(nameof(filter), filter);
		}

		// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
		// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
		// is constructed.This ensures the key is deterministic while still varying from block to block.
		public byte[] FilterKey => BlockHash.ToBytes().Take(16).ToArray();

		public string ToFullLine()
		{
			var builder = new StringBuilder();
			builder.Append(BlockHeight.ToString());
			builder.Append(":");
			builder.Append(BlockHash);
			if (Filter != null) // bech found here
			{
				builder.Append(":");
				builder.Append(Filter);
			}

			return builder.ToString();
		}

		public static FilterModel FromFullLine(string line)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
			string[] parts = line.Split(':');

			GolombRiceFilter filter;
			if (parts.Length <= 1)
			{
				throw new ArgumentException(nameof(line), line);
			}
			else if (parts.Length == 2) // no bech here
			{
				filter = new GolombRiceFilter(new byte[] { 0 });
			}
			else
			{
				var data = Encoders.Hex.DecodeData(parts[2]);
				filter = new GolombRiceFilter(data, 20, 1 << 20);
			}

			if (Height.TryParse(parts[0], out Height blockHeight))
			{
				uint256 blockHash = new uint256(parts[1]);
				return new FilterModel(blockHeight, blockHash, filter);
			}
			else
			{
				throw new FormatException($"Could not parse {nameof(Height)}.");
			}
		}
	}
}
